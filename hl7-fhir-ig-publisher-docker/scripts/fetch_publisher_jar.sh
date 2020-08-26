#! /bin/bash

# Fetch a version of the IG publisher jar from its remote storage location
set -eo pipefail
JAR_LOCATION="https://github.com/HL7/fhir-ig-publisher/releases/latest/download/publisher.jar"
echo "Fetching IG Publisher from $JAR_LOCATION"
curl -L "$JAR_LOCATION" -o org.hl7.fhir.publisher.jar
