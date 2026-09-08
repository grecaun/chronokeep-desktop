#!/bin/bash

echo ---- Looking for new Version ----
if [ -f ./chronokeep.tar.gz ]
	echo ---- New Version Found! Updating. ----
	echo ---- Decompressing Archive ----
	gunzip chronokeep.tar.gz
	echo ---- Extracting Archive ----
	tar -xf chronokeep.tar
else
	echo ---- No New Version Found ----
fi