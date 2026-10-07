#!/bin/bash
find . -name "*.cs" > source_files.txt
mcs @source_files.txt -target:library -out:Assembly-CSharp.dll -r:UnityEngine.dll
