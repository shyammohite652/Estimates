# Construction Estimator UI

Angular 22 single-page app for the Construction Estimator API. See the [root README](../../README.md)
for the full setup.

```bash
npm install
npm start                    # http://localhost:4200, proxies /api to http://localhost:5080
npx ng test --watch=false    # unit tests (Vitest)
npx ng build                 # production build into dist/
```

## Structure

```
src/app
  core/        auth (service, guard, interceptor), HTTP error interceptor, typed API client, models
  shared/      status chip, confirm dialog, form validators, role labels
  features/
    login/     seeded-user picker (development sign-in)
    projects/  assigned projects and estimates, new estimate dialog
    estimate/  estimate header, totals, paged lines, line dialog, lifecycle actions, history
```

Action buttons are driven by `capabilities` computed by the API for the signed-in user. Hiding a
button is only a convenience; every rule is enforced on the server.
