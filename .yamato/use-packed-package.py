"""Points a test project at the packed package instead of the source folder.

The source folder has no server jar: it is a build output, gitignored, and built by
the jar job whose artifact the pack job folds into the tarball. Tests that push the
jar to a device therefore need the package the pack job produced - which is also what
a user installs, so this is the thing worth testing.

Usage: python .yamato/use-packed-package.py <project path> <package name>
"""
import glob
import json
import os
import sys

project, package = sys.argv[1], sys.argv[2]

# Globbed rather than named with a version, the way the wrench validation jobs take
# their packages, so a version bump does not have to reach in here.
tarballs = glob.glob(os.path.join('upm-ci~', 'packages', package + '-*.tgz'))
if len(tarballs) != 1:
    found = ', '.join(sorted(tarballs)) or 'nothing'
    sys.exit(f"Expected one {package} tarball from the pack job, found {found}.")

manifest_path = os.path.join(project, 'Packages', 'manifest.json')
with open(manifest_path, encoding='utf-8') as f:
    manifest = json.load(f)

previous = manifest['dependencies'].get(package)
manifest['dependencies'][package] = 'file:' + os.path.abspath(tarballs[0]).replace('\\', '/')

with open(manifest_path, 'w', encoding='utf-8') as f:
    json.dump(manifest, f, indent=2)
    f.write('\n')

print(f"{manifest_path}: {package} {previous} -> {manifest['dependencies'][package]}")
