import { defineConfig } from '@playwright/test';
import { mkdtempSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { randomBytes } from 'node:crypto';

if (!process.env.AIM_TEST_ROOT) {
  process.env.AIM_TEST_ROOT = mkdtempSync(join(tmpdir(), 'aim-e2e-'));
  cpSync(resolve('fixtures/nest/src'), join(process.env.AIM_TEST_ROOT, 'src'), { recursive: true });
}
process.env.AIM_TEST_TOKEN ??= randomBytes(32).toString('hex');
const root = process.env.AIM_TEST_ROOT;
export default defineConfig({
  testDir: './tests/e2e', fullyParallel: false, workers: 1, timeout: 40_000,
  use: { baseURL: 'http://127.0.0.1:5175', headless: true, screenshot: 'only-on-failure' },
  webServer: [
    {
      command: 'dotnet run --project src/Aim.Api --no-launch-profile',
      url: 'http://127.0.0.1:5188/health', timeout: 120_000,
      env: { DOTNET_CLI_TELEMETRY_OPTOUT: '1', AIM_TOKEN: process.env.AIM_TEST_TOKEN, AIM_PORT: '5188', AIM_REPOSITORY: root,
        AIM_DATABASE: join(root, 'aim.db'), AIM_ANALYZER: resolve('analyzer/extract.mjs'), AIM_UI_ORIGIN: 'http://127.0.0.1:5175' }
    },
    {
      command: 'npm run dev --workspace frontend -- --port 5175 --strictPort',
      url: 'http://127.0.0.1:5175', timeout: 60_000,
      env: { VITE_API_URL: 'http://127.0.0.1:5188' }
    }
  ]
});
