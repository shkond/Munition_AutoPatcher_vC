# Mutagen — How to find and reference the latest API documentation

This document explains how to locate and reference the latest Mutagen API documentation (library public API), based on a local shallow clone of the Mutagen repository (`mutagen-tmp`). Mutagen's documentation is maintained with MkDocs and published as a static GitHub Pages site. There is no single static OpenAPI/Swagger file in the repo.

**Summary findings:**
- **Docs system:** MkDocs (`mkdocs.yml`) with content under `docs/`.
- **No static OpenAPI/Swagger**: repository does not contain a raw `openapi.yaml`/`swagger.json`; Mutagen is a C# library, not an HTTP REST service.
- **Primary API pages:** the MkDocs `nav` contains sections that correspond to the library API and examples, notably `Plugin Record Suite`, `Link Cache`, `Environment`, `Load Order`, `Low Level Tools`, `WPF Library`, and the site root (`index.md`) which contains a Sample API section.

**Useful local files (in your shallow clone):**
- `mutagen-tmp/mkdocs.yml` — MkDocs configuration and site navigation.
- `mutagen-tmp/docs/` — source pages (Markdown/YAML) used to generate the site.

**What this means**
- The "API" is the library surface (C# classes, interfaces, generated classes). The docs are source markdown that MkDocs converts to HTML; therefore you should consume either the generated HTML site (GitHub Pages) or the local markdown files.
- If you wanted an OpenAPI (HTTP) spec: none is provided in this repo (Mutagen is not a REST server), so there is no `openapi.yaml` to download.

**Where to look in the docs (high-priority pages)**
- `index.md` — site root; contains a "Sample API" section and links to example pages.
- `plugins/index.md` + `plugins/*` — Plugin Record Suite: generated classes and record API.
- `linkcache/index.md` + `linkcache/*` — LinkCache APIs and examples for record lookup.
- `environment/index.md` + `environment/*` — Game environment construction and API examples.
- `loadorder/index.md` — Load order and winning override API.
- `lowlevel/index.md` + `lowlevel/*` — Low-level parsing utilities and API surfaces.
- `wpf/index.md` + `wpf/*` — WPF helper library and controls API.

**Commands you can run locally (PowerShell)**
- Show `mkdocs.yml` nav quickly:

  ```powershell
  Get-Content .\mutagen-tmp\mkdocs.yml -Raw
  ```

- Read a specific doc source page (example: plugins index):

  ```powershell
  Get-Content .\mutagen-tmp\docs\plugins\index.md -Raw | Out-Host
  ```

- Serve the docs locally with MkDocs (requires Python + mkdocs + mkdocs-material):

  ```powershell
  py -m pip install --user mkdocs mkdocs-material
  # from inside mutagen-tmp (the folder with mkdocs.yml)
  cd .\mutagen-tmp
  py -m mkdocs serve
  ```

  Then open `http://127.0.0.1:8000` in your browser. This is the recommended way to browse the full generated site locally with search and navigation.

- Download the already-generated public HTML pages from the published GitHub Pages site (one-off downloads). Example PowerShell commands:

  ```powershell
  $base = 'https://mutagen-modding.github.io/Mutagen'
  # root index
  Invoke-WebRequest -Uri "$base/" -OutFile mutagen_index.html
  # plugins top (Plugin Record Suite)
  Invoke-WebRequest -Uri "$base/plugins/" -OutFile mutagen_plugins_index.html
  # linkcache
  Invoke-WebRequest -Uri "$base/linkcache/" -OutFile mutagen_linkcache_index.html
  # environment
  Invoke-WebRequest -Uri "$base/environment/" -OutFile mutagen_environment_index.html
  # lowlevel
  Invoke-WebRequest -Uri "$base/lowlevel/" -OutFile mutagen_lowlevel_index.html
  # wpf
  Invoke-WebRequest -Uri "$base/wpf/" -OutFile mutagen_wpf_index.html
  ```

  Note: adjust the paths to target deeper pages (for example `plugins/Importing-and-Construction/`). If a page ends with `index.md` in sources, browse the directory path on the site.

**Quick mapping: mkdocs nav → public site URL**
- `index.md` → `/`
- `plugins/index.md` → `/plugins/`
- `plugins/Importing-and-Construction.md` → `/plugins/Importing-and-Construction/`
- `linkcache/index.md` → `/linkcache/`
- `environment/index.md` → `/environment/`
- `lowlevel/index.md` → `/lowlevel/`
- `wpf/index.md` → `/wpf/`

**If you need the C# API types (source code)**
- The public API surface is defined in the `Mutagen.*` projects (source). Use IDE features (IntelliSense) or inspect the generated code and XML docs in the `Mutagen.Bethesda.*` projects in the repository.

**If you wanted a single compiled HTML/PDF of the API**
- Option A (quick): use the GitHub Pages site and save/print as PDF from your browser.
- Option B (local): run `mkdocs build` inside `mutagen-tmp` to produce a `_site` static directory of HTML which you can archive, serve, or convert to PDF.

**Next actions you can ask me to do**
- I can download specific pages for you (tell me which pages or nav entries). I will provide `Invoke-WebRequest` commands or fetch them if you want me to run commands locally.
- I can run `mkdocs build`/`mkdocs serve` instructions and help troubleshoot installation if you want to preview locally.
- If you really require an OpenAPI/Swagger spec for an HTTP API, I can re-scan the repo for any server or Swagger-generation code (but initial greps show none).

---
Generated from a shallow clone of Mutagen (local path `mutagen-tmp`). If you want, I can extract the exact `nav` entries and produce a shorter list of direct download URLs for the pages you care about.

---
Last inspected files (examples): `mutagen-tmp/mkdocs.yml`, `mutagen-tmp/docs/index.md`, `mutagen-tmp/docs/plugins/index.md`, `mutagen-tmp/docs/linkcache/index.md`.
