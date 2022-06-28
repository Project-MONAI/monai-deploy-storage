#!/bin/bash

# SPDX-FileCopyrightText: © 2022 MONAI Consortium
# SPDX-License-Identifier: Apache License 2.0

export PACKAGEDIR="${PWD}/release"

[ -d $PACKAGEDIR ] && rm -rf $PACKAGEDIR && echo "Removing $PACKAGEDIR..."


find . -type f -name '*.csproj' ! -name '*.Tests.csproj' -exec bash -c '
    for project do
        echo Processing $project...
        projectName=$(basename -s .csproj $project)
        projectPACKAGEDIR="${PACKAGEDIR}/${projectName}"
        zipPath="${PACKAGEDIR}/${projectName}.zip"
        mkdir -p $projectPACKAGEDIR
        echo Publishing $project...
        dotnet publish $project -c Release -o $projectPACKAGEDIR --nologo
        pushd $projectPACKAGEDIR
        rm -f Microsoft*.dll System*.dll JetBrains*.dll
        zip -r $zipPath *
        popd
    done
' _ {} +