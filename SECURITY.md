# Security Policy

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues, discussions, or pull requests.**

Instead, use GitHub's private vulnerability reporting:

1. Go to the [Security tab](https://github.com/Spectara/Revela/security) of the repository.
2. Click **Report a vulnerability** to open a private security advisory.

This keeps the report visible only to the maintainers until a fix is available.
If private reporting is unavailable to you, contact the maintainer directly via
their GitHub profile ([@kirkone](https://github.com/kirkone)) instead of filing a
public issue.

When reporting, please include:

- A description of the vulnerability and its impact.
- Steps to reproduce (a minimal proof of concept is ideal).
- Affected version or commit.
- Any suggested remediation, if you have one.

We will acknowledge your report as soon as we can and keep you updated on progress
toward a fix.

## Supported versions

Revela is **pre-release** software with no stability guarantees yet. Security fixes
are applied to the `main` branch. There are no maintained release branches.

## Scope and threat model

Revela is a **single-author static site generator**: all input to the renderer is
trusted by the same person who owns the output. The intended trust boundaries,
what Revela does and does not protect against, and upgrade paths for stricter
threat models are documented in
[`docs/security-model.md`](docs/security-model.md).
