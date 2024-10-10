.DEFAULT_GOAL := publish

ifndef EXE
	EXE = Sapling
endif

# Set a default output directory if not already defined
OUTPUT_DIR ?= ./

# Detect OS and Architecture
UNAME_S := $(shell uname -s)
UNAME_P := $(shell uname -p)

ifeq ($(OS),Windows_NT)
	RUNTIME=win-x64
	SHELL := cmd.exe
	MKDIR_CMD := if not exist "$(subst /,\,$(OUTPUT_DIR))" mkdir "$(subst /,\,$(OUTPUT_DIR))"
else
	# Default runtime for Linux/MacOS
	ifeq ($(UNAME_S),Linux)
		RUNTIME=linux-x64
		ifneq ($(filter aarch64% armv8% arm%,$(UNAME_P)),)
			RUNTIME=linux-arm64
		endif
	else ifeq ($(UNAME_S),Darwin)
		RUNTIME=osx-x64
		ifneq ($(filter arm%,$(UNAME_P)),)
			RUNTIME=osx-arm64
		endif
	endif
	SHELL := /bin/sh
	MKDIR_CMD := mkdir -p $(OUTPUT_DIR)
endif

# Publish target
publish:
	$(MKDIR_CMD)
	dotnet publish ../../src/Sapling/Sapling.csproj -c Release --runtime $(RUNTIME) --self-contained \
		-p:PublishSingleFile=true -p:DeterministicBuild=true \
		-o $(OUTPUT_DIR) -p:ExecutableName=$(EXE) -p:DefineConstants="OpenBench"
