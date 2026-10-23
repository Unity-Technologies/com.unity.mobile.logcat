using System;
using System.Collections.Generic;
using System.IO;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// When each frame of an MP4 recording appears, read from the file itself.
    /// <para>
    /// screenrecord only writes a frame when the screen changes, so the gaps between
    /// frames vary from a few milliseconds to seconds. VideoPlayer numbers frames as if
    /// the rate were constant - time times the average rate - so its frame numbers skip
    /// and repeat, and stepping by one of them can land on the same image or jump past
    /// several. The file's own timing table says where every real frame starts.
    /// </para>
    /// </summary>
    internal static class AndroidLogcatMp4Timing
    {
        /// <summary>
        /// The start time of every frame of the first video track, in seconds and in the
        /// order they are shown. Null when the file cannot be read or is not laid out
        /// as expected - the caller falls back to VideoPlayer's own numbering.
        /// </summary>
        internal static double[] ReadFrameTimes(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    // Only the index is read. The frames themselves (mdat) are most of
                    // the file, and are skipped over rather than loaded.
                    var moov = ReadTopLevelBox(stream, "moov");
                    if (moov == null)
                        return null;

                    foreach (var trak in Children(moov, 0, moov.Length, "trak"))
                    {
                        var times = ReadVideoTrack(moov, trak.Start, trak.End);
                        if (times != null)
                            return times;
                    }
                }
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log($"Failed to read the frame times of '{path}': {ex.Message}");
            }
            return null;
        }

        struct Box
        {
            internal string Type;
            internal int Start;
            internal int End;
        }

        static byte[] ReadTopLevelBox(Stream stream, string type)
        {
            var header = new byte[16];
            while (stream.Position + 8 <= stream.Length)
            {
                var boxStart = stream.Position;
                if (stream.Read(header, 0, 8) != 8)
                    return null;
                long size = ReadUInt32(header, 0);
                var name = TypeOf(header, 4);
                var headerSize = 8;
                if (size == 1)
                {
                    if (stream.Read(header, 8, 8) != 8)
                        return null;
                    size = (long)ReadUInt64(header, 8);
                    headerSize = 16;
                }
                else if (size == 0)
                {
                    size = stream.Length - boxStart;
                }
                if (size < headerSize)
                    return null;

                if (name == type)
                {
                    var body = new byte[size - headerSize];
                    var read = 0;
                    while (read < body.Length)
                    {
                        var n = stream.Read(body, read, body.Length - read);
                        if (n <= 0)
                            return null;
                        read += n;
                    }
                    return body;
                }
                stream.Position = boxStart + size;
            }
            return null;
        }

        static IEnumerable<Box> Children(byte[] data, int start, int end, string type = null)
        {
            var position = start;
            while (position + 8 <= end)
            {
                long size = ReadUInt32(data, position);
                var name = TypeOf(data, position + 4);
                var headerSize = 8;
                if (size == 1)
                {
                    size = (long)ReadUInt64(data, position + 8);
                    headerSize = 16;
                }
                else if (size == 0)
                {
                    size = end - position;
                }
                if (size < headerSize || position + size > end)
                    yield break;

                if (type == null || name == type)
                    yield return new Box { Type = name, Start = position + headerSize, End = (int)(position + size) };
                position += (int)size;
            }
        }

        static Box? Find(byte[] data, int start, int end, params string[] path)
        {
            foreach (var box in Children(data, start, end, path[0]))
            {
                if (path.Length == 1)
                    return box;
                var rest = new string[path.Length - 1];
                Array.Copy(path, 1, rest, 0, rest.Length);
                return Find(data, box.Start, box.End, rest);
            }
            return null;
        }

        static double[] ReadVideoTrack(byte[] data, int start, int end)
        {
            var hdlr = Find(data, start, end, "mdia", "hdlr");
            // version and flags, pre_defined, then the handler type.
            if (hdlr == null || TypeOf(data, hdlr.Value.Start + 8) != "vide")
                return null;

            var mdhd = Find(data, start, end, "mdia", "mdhd");
            if (mdhd == null)
                return null;
            var mdhdVersion = data[mdhd.Value.Start];
            // After version and flags, creation and modification times - 64 bit each in
            // version 1, 32 bit in version 0 - then the timescale.
            var timescale = ReadUInt32(data, mdhd.Value.Start + (mdhdVersion == 1 ? 20 : 12));
            if (timescale == 0)
                return null;

            var stbl = Find(data, start, end, "mdia", "minf", "stbl");
            if (stbl == null)
                return null;

            var stts = Find(data, stbl.Value.Start, stbl.Value.End, "stts");
            if (stts == null)
                return null;

            // Decode times: runs of (count, duration).
            var decodeTimes = new List<long>();
            long time = 0;
            var entries = ReadUInt32(data, stts.Value.Start + 4);
            for (var i = 0; i < entries; i++)
            {
                var entry = stts.Value.Start + 8 + i * 8;
                var count = ReadUInt32(data, entry);
                var duration = ReadUInt32(data, entry + 4);
                for (var j = 0; j < count; j++)
                {
                    decodeTimes.Add(time);
                    time += duration;
                }
            }

            // Shown later than decoded when frames are reordered. screenrecord does not
            // reorder, but nothing stops another recorder from doing it.
            var ctts = Find(data, stbl.Value.Start, stbl.Value.End, "ctts");
            if (ctts != null)
            {
                var cttsVersion = data[ctts.Value.Start];
                var sample = 0;
                var cttsEntries = ReadUInt32(data, ctts.Value.Start + 4);
                for (var i = 0; i < cttsEntries && sample < decodeTimes.Count; i++)
                {
                    var entry = ctts.Value.Start + 8 + i * 8;
                    var count = ReadUInt32(data, entry);
                    long offset = cttsVersion == 1 ? (int)ReadUInt32(data, entry + 4) : ReadUInt32(data, entry + 4);
                    for (var j = 0; j < count && sample < decodeTimes.Count; j++, sample++)
                        decodeTimes[sample] += offset;
                }
            }

            // An edit list can start playback part way into the media, and the player's
            // clock starts there.
            long mediaStart = 0;
            var elst = Find(data, start, end, "edts", "elst");
            if (elst != null && ReadUInt32(data, elst.Value.Start + 4) > 0)
            {
                var elstVersion = data[elst.Value.Start];
                var first = elst.Value.Start + 8;
                mediaStart = elstVersion == 1
                    ? (long)ReadUInt64(data, first + 8)
                    : (int)ReadUInt32(data, first + 4);
                // -1 is an empty edit, a gap rather than a start.
                if (mediaStart < 0)
                    mediaStart = 0;
            }

            decodeTimes.Sort();
            var times = new double[decodeTimes.Count];
            for (var i = 0; i < times.Length; i++)
                times[i] = Math.Max(0, decodeTimes[i] - mediaStart) / (double)timescale;
            return times.Length > 0 ? times : null;
        }

        static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);
        }

        static ulong ReadUInt64(byte[] data, int offset)
        {
            return (ulong)ReadUInt32(data, offset) << 32 | ReadUInt32(data, offset + 4);
        }

        static string TypeOf(byte[] data, int offset)
        {
            return new string(new[] { (char)data[offset], (char)data[offset + 1], (char)data[offset + 2], (char)data[offset + 3] });
        }
    }
}
