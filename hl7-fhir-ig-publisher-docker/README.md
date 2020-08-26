# 🔥🐳 Dockerized HL7 IG Publisher

A packaged version of the HL7 IG Publisher so that you can
run builds on your IG source files without installing all it's dependencies.

This is very useful for running the IG Publisher in CI pipelines.
(I'm using it in Azure DevOps and local builds now)

Based on the good work by https://github.com/ncpi-fhir/hl7-fhir-ig-publisher which packages up the FHIR IG Publisher with all it's build requirements.

> **Note:** I've created this version as it was a little out of date, and loading from the wrong location now (and I wanted to learn a little about how Docker works too)
>
> I'll update it occasionally to keep up with current developments (or as my projects need me to)

The image is published on the Docker Hub here:
https://hub.docker.com/repository/docker/brianpos/fhir-ig-publisher

&nbsp;

## Example Use
Run the IG publisher in a docker container with a bind mounted volume containing everything needed to build the IG

```shell
# docker run --rm -it -v <path to IG dir>:/data <docker image> <normal IG publisher CLI args here>
$ docker run --rm -it -v $(pwd)/test/ig-site:/data brianpos/fhir-ig-publisher:latest -ig /data/ig.json -tx n/a
```
If running locally, or have a shared place for the profile cache you can also mount the profile cache folder too
```shell
c:\git\test-ig> docker run --rm --name ig-publisher -v "c:\git\MySL.FhirIG:/data:rw" -v "%USERPROFILE%\.fhir:/root/.fhir" -t brianpos/fhir-ig-publisher:latest -ig /data/ig.json
```
> **Note:** The `data` and `.fhir` mounted paths to get the content into the build, and also the package cache
> (this are the Windows user folder paths, probably different on other platforms)

&nbsp;

## Building the docker image
This is how I build the image for publishing to the docker hub:
```shell
$ docker build -t brianpos/fhir-ig-publisher:latest .
```
> **Note:** When running you may need to use the `--no-cache` parameter so that it doesn't use the cached version (particularly for the step that loads the ig-publisher jar itself)

&nbsp;

## Publishing the docker image
```shell
$ docker push brianpos/fhir-ig-publisher:latest
```
In the future I may also tag with the version of the IG builder that is inside.
