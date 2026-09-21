# Changelog

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
