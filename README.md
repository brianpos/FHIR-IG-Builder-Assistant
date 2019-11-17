# FHIR-IG-Builder-Assistant
Helper project that can tweak IG artifacts assisting in preparing IGs for publishing, and also simplifying OpenAPI artifacts to make work with editor.swagger.io

## Usage:

```
dotnet FHIR IG Builder Assistant.dll -normalize
dotnet FHIR IG Builder Assistant.dll -cleanopenapi
dotnet FHIR IG Builder Assistant.dll -updatepublishbox "update with this text"
dotnet FHIR IG Builder Assistant.dll -prepare-release
```

## Normalize
This will scan over the IG folder, finding all resources, and ensuring that the appropriate markdown files exist in the `pages/
_includes` folder, and are also included in the `IG.xml` and `ig.json` files, also updating any name values in the `ig.xml`

## Clean OpenAPI
This is an internal tool that was used to cleanse the output so that it could be used with 

## Update Publish Box
This will iterate over all files in the current working directory and replace the contents of the "publish box" with the textprovided

The working folder of the application should be the output folder of the IG Publisher for this command

## Prepare-Release
The prepare release stage will perform several actions, iterating over all generated files in folders with the name of each version listed in the pacakge-list.json file in the folder, replacing the content with specific text.

Before running this tool you should run a script that copies the output files into the appropriate folders
```
rem copy the output to the release version folder(s)
xcopy output\*.* 1.0.0 /y /q /I /R /s

rem prepare the root output folder
rd root /s/q
xcopy output\*.* root /s /y /q /I /R
xcopy site-root\*.* root /s /y /q /I /R
```

For the current release folder in `{version}`
`This is the current published version in its permanent home. <a href=\"..\\history.html\">Directory of published versions</a>`

For the current release folder in `root`
`This is the current published version in its permanent home. <a href=\"..\\history.html\">Directory of published versions</a>`

For the historic release folders in `{version}`
`This version is superseded by <a href=\"..\\{igBusinessVersion}\\index.html\">{igBusinessVersion}</a>. <a href=\"..\\history.html\">Directory of published versions</a>`

`This is the continuous integration build, it is not an authorized publication, and may be broken or incomplete at times. Refer to the <a href=\"..\\history.html\">Directory of published versions</a> for stable versions`

This publishing process requires some extra folders maintained with your IG

| Folder | Description |
| --- | --- |
| `site-root` | A folder that contains all the generic files that are required in the root folder, specifically the `history.html`, and any css/js/image asset files required |
| `output` | The output folder that your IG will output its generated files into |
| `root` | The generated folder that will contain all the files from the output, plus the content of site-root |
| {version} | Each release of your guide in the package-json file that you publish needs its own output folder |

With this structure you can deploy your entire publishing repository by copying the root into the root of your web server, then each of the versioned releases under that folder also.
The CI build in `output` can then be copied under a folder call `current`.

Normal CI builds can then use the `Update Publish Box` command to just replace its content in output, and publish that folder to `current` as noted above.
