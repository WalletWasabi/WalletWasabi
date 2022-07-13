#!/bin/sh
# Compiled Python modules require a compatible Python, which means 32-bit 2.6.
export VERSIONER_PYTHON_VERSION=2.6
export DYLD_LIBRARY_PATH=.:$DYLD_LIBRARY_PATH
# Set the current working directory to the directory containing this executable,
# so that pluggable transport executables can be given with relative paths. This
# works around a change in OS X 10.9, where the current working directory is
# otherwise set to "/" when an application bundle is started from Finder.
# https://trac.torproject.org/projects/tor/ticket/10030
cd "$(dirname "$0")"
exec ./tor.real "$@"
