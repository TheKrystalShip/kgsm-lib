# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.30.0] - 2026-07-01

### Added
- `WatchdogPlayer` DTO: mirrors the watchdog's `PlayerSession` record for the `GET /players`
  endpoint response.
- `IWatchdogClient.GetAllPlayersAsync()`: fetches the live player session map from the watchdog.
  Returns `Dictionary<string, WatchdogPlayer[]>?` keyed by instance name, or `null` when the
  daemon is unreachable.

## [1.28.0] - 2026-06-30

### Added
- Initial versioned release.
