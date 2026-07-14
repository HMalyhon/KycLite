# KYC-Lite · Frontend

Vue 3 single-page app for the KYC-Lite document-verification demo. You upload an ID or passport
image, pick which fields to return and which validation rules to apply, and get an
approve/reject verdict with a reason per rule.

The app talks to the ASP.NET Core backend **only** through a typed API client — it has no idea
whether the backend extracted the document with Azure AI Document Intelligence or the offline
mock. See the [root README](../README.md) for the project-level story and the backend.

## Stack

| | |
|---|---|
| Framework | Vue 3 (`<script setup>` SFCs) + TypeScript |
| Build tool | Vite |
| UI kit | PrimeVue 4 (Aura theme) + PrimeIcons |
| Tests | Vitest |

## Getting started

Prerequisites: **Node 20+**, and the backend running on `http://localhost:5000`
(see the [root README](../README.md#running-locally) — it runs offline on the mock extractor,
no Azure account needed).

```bash
npm install
npm run dev      # http://localhost:5173 (or the next free port)
```

The dev server proxies `/api` to the backend, so the browser sees a single origin and there are
no CORS surprises.

## Scripts

| Script | What it does |
|---|---|
| `npm run dev` | Start the Vite dev server with the `/api` proxy |
| `npm run build` | Type-check (`vue-tsc`) then build to `dist/` |
| `npm run preview` | Serve the production build locally |
| `npm run test` | Run the unit tests once (Vitest) |
| `npm run test:watch` | Run the unit tests in watch mode |

## Configuration

All config is optional — the defaults work out of the box. Copy `.env.example` to `.env` to
override:

| Variable | Default | Purpose |
|---|---|---|
| `VITE_API_TARGET` | `http://localhost:5000` | Backend the **dev proxy** forwards `/api` to |
| `VITE_API_BASE` | *(empty)* | Absolute API base for the **client**. Leave empty to use the dev proxy; set it when the frontend is hosted separately from the API |

## Project structure

```
src/
  api/client.ts               # the single boundary to the backend: typed DTOs + fetch calls
  composables/
    useVerification.ts        # all screen state + orchestration
  components/
    UploadCard.vue            # file picker / drop zone
    FieldSelector.vue         # which fields to return
    FieldRuleBuilder.vue      # compose checks: field x rule (+ param)
    ResultPanel.vue           # verdict, extracted fields, per-rule results
  lib/dateParam.ts            # advisory hints for relative date params (today, today-18y)
  App.vue                     # thin view that composes the components
  main.ts                     # bootstrap: PrimeVue + Aura theme, OS dark-mode sync
  style.css                   # global styles
```

## How it works

**The UI is discovery-driven.** On mount the app loads three catalogs from the backend:

- `GET /api/fields` — the fields you can request, each tagged with a type (`text` / `date`)
- `GET /api/field-rules` — the available rules and which field types each applies to (the matrix)
- `GET /api/default-checks` — the check set the UI seeds with

The field checkboxes and the field→rule matrix are rendered from those responses, so **adding a
field or a rule on the backend requires no frontend change** — it just shows up.

Submitting posts a multipart request to `POST /api/verify` (the image, the selected fields, and
the composed `fieldChecks`), and the response drives `ResultPanel`.

**State lives in one place.** `useVerification` owns everything — loading the catalogs, the
selected fields, the check rows, deriving the `fieldChecks` payload, and calling the API. That
keeps `App.vue` a thin view over the four components.

## Styling

Components are styled with **PrimeVue design tokens** (`var(--p-*)`) in scoped CSS rather than
hard-coded colors, so the theme stays consistent. Dark mode follows the OS: `main.ts` toggles a
`.dark` class on `<html>`, which is the selector the Aura preset emits its dark tokens under.

PrimeVue components are imported per-file (not registered globally) so they tree-shake.
