VPATH:= . BoidsVulkan/shader_objects BoidsVulkan builds
all: run

run: base.vert.spv base.frag.spv
	dotnet run

%.spv: %
	glslc $< -o BoidsVulkan/shader_objects/$@

build: win-x64.zip linux-x64.zip osx-arm64.zip

%.zip: 
	mkdir -p builds
	dotnet publish --self-contained -r $(basename $@)
	cp -r bin/Release/net9.0/$(basename $@)/publish builds/$(basename $@)
	cd builds/$(basename $@); zip -r $@ ./**
	mv builds/$(basename $@)/$@ builds
	rm -rf builds/$(basename $@)

.PHONY: run build
