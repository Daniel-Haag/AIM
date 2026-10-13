import { test, expect } from '@playwright/test';
import { readFile, writeFile, unlink } from 'node:fs/promises';
import { join } from 'node:path';
import { randomUUID } from 'node:crypto';

const api = 'http://127.0.0.1:5188';
const headers = () => ({ Authorization: `Bearer ${process.env.AIM_TEST_TOKEN}` });

test('API exige autenticação e recusa traversal e origem externa', async ({ request }) => {
  expect((await request.get(`${api}/api/investigations`)).status()).toBe(401);
  expect((await request.get(`${api}/api/investigations`, { headers: { ...headers(), Origin: 'https://example.com' } })).status()).toBe(403);
  const response = await request.post(`${api}/api/investigations`, { headers: headers(), data: { name: 'Inválida', path: '../private.ts' } });
  expect(response.status()).toBe(400);
});

test('grafo incremental, evidência, simulação e mudança real chegam ao navegador por SSE', async ({ page, request }) => {
  await page.goto('/');
  await page.getByLabel('Token local').fill(process.env.AIM_TEST_TOKEN!);
  await page.getByRole('button', { name: 'Conectar', exact: true }).click();
  await page.getByLabel('Nome da investigação').fill('Fluxo de pedidos');
  await page.getByLabel('Arquivo inicial').fill('src/app.module.ts');
  await page.getByRole('button', { name: 'Iniciar investigação' }).click();
  await expect(page.getByTestId('graph-summary')).toContainText('2 componentes · 1 relação');
  await page.getByText('OrdersModule', { exact: true }).first().click();
  await page.getByRole('button', { name: 'Investigar este arquivo' }).click();
  await expect(page.getByTestId('graph-summary')).toContainText('3 componentes · 2 relações');
  await page.getByRole('button', { name: 'Ver evidência AppModule → OrdersModule' }).click();
  await expect(page.getByTestId('evidence')).toContainText('src/app.module.ts:4');
  await expect(page.getByTestId('evidence')).toContainText('SHA-256');
  await expect(page.getByTestId('stream-status')).toHaveText('Ao vivo');

  const list = await (await request.get(`${api}/api/investigations`, { headers: headers() })).json();
  const id = list.find((i: { name: string }) => i.name === 'Fluxo de pedidos').id;
  const event = { eventId: randomUUID(), path: 'src/audit.module.ts', source: 'simulated-agent' };
  const once = await (await request.post(`${api}/api/investigations/${id}/events`, { headers: headers(), data: event })).json();
  const twice = await (await request.post(`${api}/api/investigations/${id}/events`, { headers: headers(), data: event })).json();
  expect(twice.revision).toBe(once.revision);
  await expect(page.getByTestId('graph-summary')).toContainText('4 componentes · 2 relações');
  await expect(page.getByTestId('history')).toContainText('Agente simulado');

  const file = join(process.env.AIM_TEST_ROOT!, 'src/orders.module.ts');
  const original = await readFile(file, 'utf8');
  try {
    await writeFile(file, original.replace("import { UsersModule }", "import { AuditModule } from './audit.module';\nimport { UsersModule }").replace('imports: [UsersModule]', 'imports: [UsersModule, AuditModule]'));
    await expect(page.getByTestId('graph-summary')).toContainText('4 componentes · 3 relações', { timeout: 15000 });
    await page.getByRole('button', { name: 'Ver evidência OrdersModule → AuditModule' }).click();
    await expect(page.getByTestId('evidence')).toContainText('src/orders.module.ts:5');
    await expect(page.getByTestId('history')).toContainText('Sistema de arquivos');
    await writeFile(file, original);
    await expect(page.getByTestId('graph-summary')).toContainText('4 componentes · 2 relações', { timeout: 15000 });
    await page.reload();
    await page.getByRole('button', { name: 'Fluxo de pedidos', exact: true }).click();
    await expect(page.getByTestId('graph-summary')).toContainText('4 componentes · 2 relações');
    await page.screenshot({ path: 'test-results/aim-poc.png', fullPage: true });
  } finally { await writeFile(file, original); }
});

test('remoção de fonte apaga relações; sintaxe inválida exibe diagnóstico recuperável', async ({ request }) => {
  const localHeaders = headers();
  const created = await (await request.post(`${api}/api/investigations`, { headers: localHeaders, data: { name: 'Recuperação', path: 'src/orders.module.ts' } })).json();
  const file = join(process.env.AIM_TEST_ROOT!, 'src/orders.module.ts');
  const original = await readFile(file, 'utf8');
  const graph = async () => (await request.get(`${api}/api/investigations/${created.id}`, { headers: localHeaders })).json();
  try {
    await writeFile(file, '@Module({ imports: [');
    await expect.poll(async () => (await graph()).edges.length, { timeout: 15000 }).toBe(0);
    expect((await graph()).diagnostics.join(' ')).toContain('Análise indisponível');
    await writeFile(file, original);
    await expect.poll(async () => (await graph()).edges.length, { timeout: 15000 }).toBe(1);
    await unlink(file);
    await expect.poll(async () => (await graph()).edges.length, { timeout: 15000 }).toBe(0);
  } finally { await writeFile(file, original); }
});
