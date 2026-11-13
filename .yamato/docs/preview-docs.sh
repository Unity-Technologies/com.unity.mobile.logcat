#!/bin/sh
# Serves the docs over HTTP: DocFX loads the sidebar with a request that browsers block for pages opened from disk.
command -v python3 >/dev/null 2>&1 || { echo "Python 3 is required to serve the docs."; exit 1; }

cd "$(dirname "$0")/documentation" || exit 1
for d in com.unity.mobile.android-logcat@*; do
  if [ "$d" != "com.unity.mobile.android-logcat@latest" ]; then DOCS=$d; fi
done

URL="http://localhost:8000/$DOCS/manual/index.html"
(sleep 1; if command -v open >/dev/null 2>&1; then open "$URL"; else xdg-open "$URL"; fi) &
echo "Serving $DOCS at http://localhost:8000, press Ctrl+C to stop."
python3 -m http.server 8000
