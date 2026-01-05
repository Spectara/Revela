#!/bin/bash
# macOS launcher for Revela standalone mode
# Double-click this file to start Revela with correct working directory
cd "$(dirname "$0")"
./revela "$@"
