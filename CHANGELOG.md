# Changelog

## [0.3.0](https://github.com/danganhphu/scdl-4life/compare/v0.2.0...v0.3.0) (2026-09-24)


### Features

* **install:** one line install for all three platforms ([67c30c4](https://github.com/danganhphu/scdl-4life/commit/67c30c4324426bf6c1e907b458e2cf7d10ea05ea))


### Fixes

* **cli:** say when a token was passed and the ceiling still did not unlock ([cbc226f](https://github.com/danganhphu/scdl-4life/commit/cbc226f99af159184f71f703724230f119704a01))

## [0.2.0](https://github.com/danganhphu/scdl-4life/compare/v0.1.0...v0.2.0) (2026-09-22)


### Features

* **release:** publish an osx-arm64 build ([8cb50a5](https://github.com/danganhphu/scdl-4life/commit/8cb50a5b2c3358edbbd1eb4b3460bb6b0db41deb))


### Fixes

* **release:** stage the binary, not the symbol table macOS hides beside it ([879cea1](https://github.com/danganhphu/scdl-4life/commit/879cea1d75d0c9cf26a36d2d3b4b60950648cb8a))

## 0.1.0 (2026-09-21)


### Features

* add scdl, a SoundCloud CLI that reports real bitrates ([fef8207](https://github.com/danganhphu/scdl-4life/commit/fef82078c9980470b9dd80e15431a136a564534a))
* **options:** validate SoundCloudOptions with the source-generated validator ([159989f](https://github.com/danganhphu/scdl-4life/commit/159989f3626d221eda9efbf0725c9735176a11a2))
* **release:** cut releases from merges instead of hand-typed tags ([5466317](https://github.com/danganhphu/scdl-4life/commit/54663172764856eb326927a9dcdab8eb58744f1d))
* **release:** version from the git tag, checksums and provenance ([#7](https://github.com/danganhphu/scdl-4life/issues/7)) ([ead96cf](https://github.com/danganhphu/scdl-4life/commit/ead96cfb01e2ff63ea6ff11c0fef7745bfd7ca3b))
* tag sets with their album and running order ([#6](https://github.com/danganhphu/scdl-4life/issues/6)) ([423f5e7](https://github.com/danganhphu/scdl-4life/commit/423f5e7811002eec637d00d2c352242c28cae9b9))


### Fixes

* **build:** stop probing for MSVC outside Windows ([2c799ec](https://github.com/danganhphu/scdl-4life/commit/2c799ece1e5ef3d683b114554a10ba8b847e64cd))
* **hls:** tell ffmpeg the container instead of letting it read the .part name ([#5](https://github.com/danganhphu/scdl-4life/issues/5)) ([28f352f](https://github.com/danganhphu/scdl-4life/commit/28f352f75ab7ad5ef34bbf99ed78100272cc2706))
* **progress:** measure a mux in stream time, not in bytes it has not written yet ([ae4be0f](https://github.com/danganhphu/scdl-4life/commit/ae4be0f1a948c95c17f90f3cebc39197e2645045))
* **progress:** report the mux and stop claiming 100% on an unknown total ([0c26135](https://github.com/danganhphu/scdl-4life/commit/0c2613548929cf38170fc5945ad3c71bdad5e4ff))
* **release:** pin the first release to 0.1.0 and correct the docs ([f5ec67f](https://github.com/danganhphu/scdl-4life/commit/f5ec67f2ee8b9437dff92093ecb86f24f7092540))
