# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Actual Bounding Box Drawing**: Replaced placeholder with production-ready implementation using SkiaSharp
  - Configurable box colors, thickness, and rounded corners
  - Label rendering with class name and confidence scores
  - Support for per-class colors and deterministic color generation
  - Cross-platform support (Windows, Linux, macOS)
  - 10 new unit tests for drawing functionality

- **Sophisticated Video Processing Options**: Extended video processing pipeline with configurable options
  - `--fps`: Target output FPS with frame resampling
  - `--resize`: Target resolution with aspect ratio preservation and letterboxing
  - `--grayscale`: Grayscale conversion option
  - `--codec`: Video codec selection (default: libx264)
  - `--quality`: Quality parameter (CRF value)
  - `--motion-detect`: Motion detection pre-filter to skip similar frames
  - 5 new unit tests for processing options

- **Enhanced Error Handling and Retry Logic**: Robust retry mechanism for network and IO operations
  - Exponential backoff with configurable jitter
  - Per-attempt timeouts
  - Smart retry logic (only transient errors like network timeouts, 5xx HTTP)
  - Configurable max attempts and delay parameters
  - Applied to Moondream API calls and Azure Blob Storage operations
  - 13 new unit tests for retry logic

- **Telemetry and Monitoring**: Basic telemetry infrastructure
  - File-based telemetry collector (JSON lines format)
  - Prometheus-style metrics via `/metrics` endpoint
  - Configurable telemetry (can be disabled)
  - Metrics include counters, gauges, and histograms

### Changed
- `VideoProcessRequest` and `FrameBatchProcessRequest` now accept optional `ProcessingOptions` parameter
- `MoondreamService` now uses retry logic for API calls
- `VideoProcessingService` now uses retry logic for blob storage operations
- All services registered with dependency injection

### Technical Details
- **Total Tests**: 51 (all passing)
  - Bounding Box Drawing: 10 tests
  - Video Processing Options: 5 tests
  - Retry Logic: 13 tests
  - Existing Tests: 23 tests
- **New Dependencies**:
  - SkiaSharp 2.88.8 (cross-platform image processing)
  - SkiaSharp.NativeAssets.Linux 2.88.8 (for Linux support)
- **Backward Compatibility**: All changes are backward compatible. New features are opt-in via configuration or request parameters.

## [1.0.0] - Initial Release

See README.md for initial feature set.
