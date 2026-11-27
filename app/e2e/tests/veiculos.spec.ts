import { expect, test, type Locator, type Page } from '@playwright/test';

test('frota sem foto: cadastro, erros, filtro, agrupamento e exclusão por teclado', async ({ page, request }) => {
  test.setTimeout(120_000);
  const api = 'http://127.0.0.1:5285/api/v1';
  const usuario = `frota${Date.now().toString(36)}`;
  const senha = 'abcdefgh';
  const registro = await request.post(`${api}/registro`, {
    data: { username: usuario, email: `${usuario}@example.invalid`, password: senha },
  });
  expect(registro.status()).toBe(201);
  await page.goto('/entrar');
  await tabAte(page, page.locator('#usuario'));
  await page.keyboard.type(usuario);
  await page.keyboard.press('Tab');
  await expect(page.locator('#senha')).toBeFocused();
  await page.keyboard.type(senha);
  await tabAte(page, page.getByRole('button', { name: 'Entrar', exact: true }));
  const login = page.waitForResponse((resposta) => resposta.url().endsWith('/session/login') && resposta.request().method() === 'POST');
  await page.keyboard.press('Enter');
  const respostaLogin = await login;
  expect(respostaLogin.status()).toBe(200);
  const headers = { Authorization: `Bearer ${(await respostaLogin.json()).accessToken}` };
  await expect(page).toHaveURL(/\/painel$/);

  const ids: string[] = [];
  for (const nome of ['Compactos sintéticos', 'Executivos sintéticos', 'Grupo vazio']) {
    const grupo = await request.post(`${api}/grupos`, { headers, data: { nome } });
    expect(grupo.status()).toBe(201);
    ids.push((await grupo.json()).id);
  }
  await tabAte(page, page.getByRole('link', { name: 'Veículos', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'Nenhum veículo para exibir' })).toBeVisible();
  await tabAte(page, page.getByRole('button', { name: '＋ Novo veículo' }));
  await page.keyboard.press('Enter');
  const dialogo = page.getByRole('dialog');
  await expect(page.getByLabel('Placa', { exact: true })).toBeFocused();
  await expect(dialogo.locator('input[type="file"]')).toHaveCount(1);
  await expect(dialogo.locator('img')).toHaveCount(0);
  await expect(dialogo).toContainText('Se selecionada, será enviada ao clicar em Salvar veículo');
  await tabAte(page, dialogo.getByRole('button', { name: 'Salvar veículo', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.locator('#erro-editor-veiculos')).toBeFocused();
  await expect(page.getByLabel('Placa', { exact: true })).toHaveAttribute('aria-invalid', 'true');
  await tabAte(page, page.getByLabel('Placa', { exact: true }));
  await page.keyboard.type('ABC-1D23');
  await page.keyboard.press('Tab');
  await expect(page.locator('#grupo-veiculo')).toBeFocused();
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Marca', { exact: true })).toBeFocused();
  await page.keyboard.type('Marca sintética');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Modelo', { exact: true })).toBeFocused();
  await page.keyboard.type('Modelo compacto');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Cor', { exact: true })).toBeFocused();
  await page.keyboard.type('Azul');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Ano', { exact: true })).toBeFocused();
  await page.keyboard.type('2026');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Combustível', { exact: true })).toBeFocused();
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Capacidade (litros)')).toBeFocused();
  await page.keyboard.type('48,125');
  await tabAte(page, dialogo.getByRole('button', { name: 'Salvar veículo', exact: true }));
  await page.keyboard.press('Enter');
  await expect(dialogo).toContainText('Veículo salvo. A foto é opcional');
  await expect(page.locator('#selecionar-foto')).toBeFocused();
  await tabAte(page, dialogo.getByRole('button', { name: 'Fechar', exact: true }));
  await page.keyboard.press('Enter');
  await expect(dialogo).not.toBeVisible();
  await expect(page.getByRole('row').filter({ hasText: 'ABC-1D23' })).toContainText('Gás');

  const segundo = await request.post(`${api}/veiculos`, { headers, data: {
    placa: 'XYZ-9A87', marca: 'Marca sintética', cor: 'Prata', modelo: 'Modelo executivo',
    combustivel: 'Diesel', capacidadeLitros: '50', ano: 2025, grupoId: ids[1],
  } });
  expect(segundo.status()).toBe(201);
  await tabAte(page, page.getByRole('button', { name: 'Limpar filtro', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByRole('row').filter({ hasText: 'XYZ-9A87' })).toBeVisible();

  await tabAte(page, page.getByRole('button', { name: 'Editar veículo ABC-1D23', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByLabel('Placa', { exact: true })).toBeFocused();
  await expect(page.getByLabel('Capacidade (litros)')).toHaveValue('48,125');
  await page.keyboard.press('ControlOrMeta+A');
  await page.keyboard.type('XYZ-9A87');
  await tabAte(page, dialogo.getByRole('button', { name: 'Salvar veículo', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.locator('#erro-editor-veiculos')).toBeFocused();
  await expect(page.locator('#erro-placa-veiculo')).toContainText(/cadastro|valor/i);
  await page.keyboard.press('Escape');
  await expect(page.getByRole('button', { name: 'Editar veículo ABC-1D23', exact: true })).toBeFocused();

  await tabAte(page, page.locator('#filtro-grupo'));
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Tab');
  await expect(page.getByRole('row').filter({ hasText: 'ABC-1D23' })).toBeVisible();
  await expect(page.getByRole('row').filter({ hasText: 'XYZ-9A87' })).toHaveCount(0);
  await tabAte(page, page.locator('#filtro-grupo'));
  await page.keyboard.press('End');
  await page.keyboard.press('Tab');
  await expect(page.getByRole('heading', { name: 'Nenhum veículo para exibir' })).toBeVisible();
  await expect(page.getByText('Não há veículos ativos no grupo selecionado.')).toBeVisible();
  await tabAte(page, page.getByRole('button', { name: 'Limpar filtro', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByRole('row').filter({ hasText: 'XYZ-9A87' })).toBeVisible();
  await tabAte(page, page.getByRole('checkbox', { name: 'Agrupar por grupo' }));
  await page.keyboard.press('Space');
  await expect(page.getByRole('heading', { name: /Compactos sintéticos/ })).toBeVisible();
  await expect(page.getByRole('heading', { name: /Executivos sintéticos/ })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Compactos sintéticos', exact: true })).toContainText('ABC-1D23');
  await expect(page.getByRole('region', { name: 'Executivos sintéticos', exact: true })).toContainText('XYZ-9A87');

  await tabAte(page, page.getByRole('button', { name: 'Editar veículo ABC-1D23', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByLabel('Placa', { exact: true })).toBeFocused();
  await tabAte(page, page.getByLabel('Cor', { exact: true }));
  await page.keyboard.press('ControlOrMeta+A');
  await page.keyboard.type('Verde');
  await tabAte(page, dialogo.getByRole('button', { name: 'Salvar veículo', exact: true }));
  await page.keyboard.press('Enter');
  await expect(dialogo).toContainText('Veículo salvo. A foto é opcional');
  await tabAte(page, dialogo.getByRole('button', { name: 'Fechar', exact: true }));
  await page.keyboard.press('Enter');
  await expect(dialogo).not.toBeVisible();
  await expect(page.getByRole('row').filter({ hasText: 'ABC-1D23' })).toContainText('Verde');

  for (const placa of ['ABC-1D23', 'XYZ-9A87']) {
    await tabAte(page, page.getByRole('button', { name: `Excluir veículo ${placa}`, exact: true }));
    await page.keyboard.press('Enter');
    await expect(page.getByRole('row').filter({ hasText: placa })).toHaveCount(0);
  }
  await expect(page.getByRole('heading', { name: 'Nenhum veículo para exibir' })).toBeVisible();
});

async function tabAte(page: Page, destino: Locator): Promise<void> {
  await expect(destino).toBeVisible();
  for (let indice = 0; indice < 70; indice++) {
    if (await destino.evaluate((elemento) => elemento === document.activeElement)) return;
    await page.keyboard.press('Tab');
  }
  throw new Error(`Destino não alcançado por Tab: ${destino}`);
}
