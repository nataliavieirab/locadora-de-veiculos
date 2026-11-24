import { expect, test, type Page } from '@playwright/test';

test('lista de aluguéis carrega protegida e aceita filtros de histórico', async ({ page, request }) => {
  test.setTimeout(120_000);
  const usuario = `alug${Date.now().toString(36)}`;
  const registro = await request.post('http://127.0.0.1:5285/api/v1/registro', {
    data: { username: usuario, email: `${usuario}@example.invalid`, password: 'abcdefgh' },
  });
  expect(registro.status()).toBe(201);
  await page.goto('/alugueis');
  await expect(page).toHaveURL(/\/entrar$/);
  await entrar(page, usuario);
  await page.getByRole('link', { name: 'Aluguéis', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Aluguéis', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Nenhum aluguel cadastrado' })).toBeVisible();
  await expect(page.getByText('Estimativa e valor final são diferentes.', { exact: true })).toBeVisible();
  await expect(page.getByLabel('Filtros dos aluguéis')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Nova locação' }).first()).toBeVisible();
  const texto = await page.locator('main').innerText();
  expect(texto.toLowerCase()).not.toContain('devolução conclu');
  await page.getByRole('button', { name: 'Aplicar filtros' }).click();
  await expect(page.getByText('Horários de São Paulo')).toBeVisible();
});

async function entrar(page: Page, usuario: string): Promise<void> {
  await page.getByLabel('Usuário', { exact: true }).fill(usuario);
  await page.getByLabel('Senha', { exact: true }).fill('abcdefgh');
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page).toHaveURL(/\/painel$/);
}
