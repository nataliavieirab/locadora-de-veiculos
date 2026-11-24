import { expect, test, type Page } from '@playwright/test';

test('shell cadastral protege rotas, navega em desktop/mobile e exige reautenticação após recarga', async ({ page, request }) => {
  test.setTimeout(120_000);
  const usuario = `shell${Date.now().toString(36)}`;
  const registro = await request.post('http://127.0.0.1:5285/api/v1/registro', {
    data: { username: usuario, email: `${usuario}@example.invalid`, password: 'abcdefgh' },
  });
  expect(registro.status()).toBe(201);
  const destinos = [
    ['grupos-planos', 'Grupos e planos'], ['veiculos', 'Veículos'],
    ['clientes', 'Clientes e condutores'], ['precos-servicos', 'Preços e serviços'],
    ['alugueis', 'Aluguéis'],
  ];
  for (const [rota] of destinos) {
    await page.goto(`/${rota}`);
    await expect(page).toHaveURL(/\/entrar$/);
  }
  await entrar(page, usuario);
  await page.getByRole('link', { name: 'Grupos e planos', exact: true }).click();
  for (const largura of [1280, 390]) {
    await page.setViewportSize({ width: largura, height: 844 });
    for (const [rota, nome] of destinos) {
      const navegacao = page.getByRole('navigation', { name: 'Navegação cadastral' });
      const link = navegacao.getByRole('link', { name: nome, exact: true });
      await expect(link).toBeVisible();
      await link.click();
      await expect(page).toHaveURL(new RegExp(`/${rota}$`));
      await expect(page.getByRole('heading', { name: nome, exact: true })).toBeVisible();
      await expect(navegacao.getByRole('link', { name: nome, exact: true })).toHaveAttribute('aria-current', 'page');
    }
  }
  expect(await page.evaluate(() => ({
    local: localStorage.length, sessao: sessionStorage.length, cookie: document.cookie,
  }))).toEqual({ local: 0, sessao: 0, cookie: '' });
  await page.reload();
  await expect(page).toHaveURL(/\/entrar$/);
  await entrar(page, usuario);
  await page.getByRole('link', { name: 'Preços e serviços', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Preços e serviços', exact: true })).toBeVisible();
});

async function entrar(page: Page, usuario: string): Promise<void> {
  await page.getByLabel('Usuário', { exact: true }).fill(usuario);
  await page.getByLabel('Senha', { exact: true }).fill('abcdefgh');
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page).toHaveURL(/\/painel$/);
}
