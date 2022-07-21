#!/bin/bash
# Copyright 2022 MONAI Consortium
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
# http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.


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