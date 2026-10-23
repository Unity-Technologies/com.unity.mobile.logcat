using System;
using System.Collections.Generic;
using System.IO;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// The frames of a recording as the file has them, and how they line up with what
    /// VideoPlayer can show. Nothing here plays anything.
    /// <para>
    /// screenrecord only writes a frame when the screen changes, so the gaps between
    /// frames vary from a few milliseconds to seconds. VideoPlayer numbers frames as if
    /// the rate were constant - time times the average rate - so its frame numbers skip
    /// and repeat, and stepping by one of them can land on the same image or jump past
    /// several. The file's own timing table says where every real frame starts, and the
    /// frame numbers here are indices into it.
    /// </para>
    /// </summary>
    internal class AndroidLogcatVideoInformation
    {
        readonly double[] m_FrameTimes;

        // Worked out for one frame rate at a time, which is the player's and does not
        // change while a video is open.
        double m_ShowableRate;
        long[] m_Showable;

        AndroidLogcatVideoInformation(double[] frameTimes)
        {
            m_FrameTimes = frameTimes;
        }

        /// <summary>
        /// Reads <paramref name="path"/>. Null when the file cannot be read or is not
        /// laid out as expected - the caller falls back to VideoPlayer's own numbering.
        /// </summary>
        internal static AndroidLogcatVideoInformation Load(string path)
        {
            var times = ReadFrameTimes(path);
            return times != null ? new AndroidLogcatVideoInformation(times) : null;
        }

        internal int FrameCount => m_FrameTimes.Length;

        /// <summary>When <paramref name="frame"/> starts, in seconds. Clamped to the video.</summary>
        internal double TimeOf(long frame)
        {
            return m_FrameTimes[Clamp(frame)];
        }

        /// <summary>
        /// The frame whose start is nearest <paramref name="time"/>. VideoPlayer's clock
        /// is not a frame's own start time: it runs on a constant-rate grid, frame number
        /// over average rate, and each real frame takes the grid slot its start rounds
        /// to. Frame 1 of a recording starting at 0.044 s is reported at 0.036 s, before
        /// it starts - so the nearest frame, not the last one started, is the one shown.
        /// </summary>
        internal long FrameAt(double time)
        {
            var index = Array.BinarySearch(m_FrameTimes, time);
            if (index >= 0)
                return index;
            var next = ~index;
            if (next == 0)
                return 0;
            if (next >= m_FrameTimes.Length)
                return m_FrameTimes.Length - 1;
            return time - m_FrameTimes[next - 1] <= m_FrameTimes[next] - time ? next - 1 : next;
        }

        /// <summary>
        /// The frames VideoPlayer can show at <paramref name="frameRate"/>, ascending. It
        /// shows, for each slot of its grid, the frame starting nearest it - so two frames
        /// closer together than one slot share it, and only one of them is ever on screen.
        /// Back to back screen changes are common: about one frame in ten, on a typical
        /// recording. Null until the rate is known.
        /// </summary>
        internal long[] Showable(double frameRate)
        {
            if (frameRate <= 0)
                return null;
            if (m_Showable != null && m_ShowableRate == frameRate)
                return m_Showable;

            var showable = new List<long>();
            for (var i = 0; i < m_FrameTimes.Length; i++)
            {
                var shown = ShownFor(i, frameRate);
                if (showable.Count == 0 || showable[showable.Count - 1] != shown)
                    showable.Add(shown);
            }
            m_ShowableRate = frameRate;
            m_Showable = showable.ToArray();
            return m_Showable;
        }

        /// <summary>
        /// The frame the player shows on its way to <paramref name="frame"/>: the frame
        /// itself, unless it shares a slot with a neighbour that is shown instead. A seek
        /// is sent there, so that it lands on the frame it says it will.
        /// </summary>
        internal long Snap(long frame, double frameRate)
        {
            var clamped = Clamp(frame);
            return frameRate > 0 ? ShownFor(clamped, frameRate) : clamped;
        }

        /// <summary>
        /// The next showable frame from <paramref name="from"/> in
        /// <paramref name="direction"/>, or -1 at that end of the video. Without the rate
        /// every frame counts as showable.
        /// </summary>
        internal long StepFrom(long from, int direction, double frameRate)
        {
            var showable = Showable(frameRate);
            if (showable == null)
            {
                var to = from + direction;
                return to >= 0 && to < m_FrameTimes.Length ? to : -1;
            }

            var index = Array.BinarySearch(showable, from);
            var next = index >= 0
                ? index + direction
                : direction > 0 ? ~index : ~index - 1;
            return next >= 0 && next < showable.Length ? showable[next] : -1;
        }

        /// <summary>
        /// VideoPlayer's own frame number for <paramref name="frame"/>: the slot of its
        /// constant-rate grid that the frame's start rounds to. Seeking there lands on
        /// it - seeking to the start time instead lands on the slot after.
        /// </summary>
        internal long SlotOf(long frame, double frameRate)
        {
            return (long)Math.Round(TimeOf(frame) * frameRate);
        }

        long ShownFor(long frame, double frameRate)
        {
            return FrameAt(SlotOf(frame, frameRate) / frameRate);
        }

        long Clamp(long frame)
        {
            return Math.Max(0, Math.Min(m_FrameTimes.Length - 1, frame));
        }

        /// <summary>
        /// The start time of every frame of the first video track, in seconds and in the
        /// order they are shown. Null when the file cannot be read or is not laid out
        /// as expected.
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
