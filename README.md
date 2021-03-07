# MirrorState
An experimental snapshot interpolation system built on top of the Mirror networking API for Unity.

## Purpose
Mirror doesn't include any tick-based system. It relies on SyncVar and RPCs to handle syncronization and events. This is fine for less competitive games or games with simpler states,
but many of my projects have required tick aligned updates to properly synchronize more complex states and handle rollbacks, which just isn't possible in Mirror by default. 
I decided to research how to do build this on my own.

Note that currently it's much more a proof of concept than a fully usable library. It was built piece by piece as I researched how to properly build a tick-based system, how 
snapshot interpolation works, how to handle client prediction, and more.

### Mirror
Due to constantly changing APIs with each new version of Mirror I've elected to include a specific version with this. In time I would like to move this to Mirage, which uses UPM to publish updates rather than the Asset Store.

## Current State
MirrorState is in the middle of a rewrite and isn't fully functional at the moment. Version 1 worked well enough to show off but had a number of flaws as I was (and am) still learning.
I am currently reworking parts of the system based off of Fredrik Holmstrom's video series and [accompanying code](https://github.com/fholm/NetCodeTalkStreams) on snapshot interpolation.