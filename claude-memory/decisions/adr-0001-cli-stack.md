# ADR 0001 - Parsing is System.CommandLine, rendering is Spectre.Console

**Status:** accepted · **Date:** 2026-09-18

## Context

The CLI needs argument parsing and it needs to draw a table and a progress bar.
Spectre.Console ships both halves, so using it for everything is the obvious
first move.

## Decision

Parse with **System.CommandLine 2.0**. Render with **Spectre.Console**, and only
its simple renderables.

## Why

`Spectre.Console.Cli` is documented by its own maintainers as neither trimmable
nor AOT appropriate, and unlikely to change. Native AOT is a hard requirement
here, so the parsing half was never available to us.

Splitting the two costs one extra package reference and buys a published binary
that works.

## Alternatives rejected

**Terminal.Gui** and **Hex1b** were both considered when the question came up
again. Both are full TUI frameworks - windows, focus, keyboard routing, an event
loop. scdl is a batch CLI: it runs a command, prints, and exits. Adopting either
would mean inventing an interactive mode nobody asked for in order to justify
the dependency, and adding a large AOT unknown to a project whose first
requirement is AOT.

If an interactive mode is ever genuinely wanted - browsing a set, picking rungs
by keyboard - reopen this. At that point Terminal.Gui (mature 2.x) over Hex1b
(young, targeting preview SDKs). But that is a product decision, not a library
one.
