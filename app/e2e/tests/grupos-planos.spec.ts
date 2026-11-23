import { expect, test, type Locator, type Page } from '@playwright/test';

const api = 'http://127.0.0.1:5285';

test('grupos e três planos por teclado, recriação com novo ID e isolamento A/B', async ({ page, browser, request }) => {
  test.setTimeout(120_000);
  const sufixo = Date.now().toString(36);
  const senha = 'abcdefgh';
  const usuarioA = `grupos${sufixo}a`;
  const usuarioB = `grupos${sufixo}b`;
  for (const usuario of [usuarioA, usuarioB]) {
    const registro = await request.post(`${api}/api/v1/registro`, {
      data: { username: usuario, email: `${usuario}@example.invalid`, password: senha },
    });
    expect(registro.status()).toBe(201);
  }

  await entrarPorTeclado(page, usuarioA, senha);
  await tabAte(page, page.getByRole('link', { name: 'Grupos e planos' }));
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'Nenhum grupo cadastrado' })).toBeVisible();
  await tabAte(page, page.getByRole('button', { name: '＋ Novo grupo' }));
  await page.keyboard.press('Enter');
  const dialogo = page.getByRole('dialog');
  await expect(page.getByLabel('Nome do grupo')).toBeFocused();
  await tabAte(page, dialogo.getByRole('button', { name: 'Salvar grupo', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.locator('#erro-editor-grupos')).toBeFocused();
  await expect(page.getByLabel('Nome do grupo')).toHaveAttribute('aria-invalid', 'true');
  await tabAte(page, page.getByLabel('Nome do grupo'));
  await page.keyboard.type('Compactos sintéticos');
  await tabAte(page, dialogo.getByRole('button', { name: 'Salvar grupo', exact: true }));
  await page.keyboard.press('Enter');
  await expect(dialogo).not.toBeVisible();
  await expect(page.getByRole('heading', { name: 'Compactos sintéticos', exact: true })).toBeVisible();

  await tabAte(page, page.getByRole('button', { name: 'Editar grupo Compactos sintéticos', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByLabel('Nome do grupo')).toBeFocused();
  await page.keyboard.press('ControlOrMeta+A');
  await page.keyboard.type('Compactos urbanos');
  await tabAte(page, dialogo.getByRole('button', { name: 'Salvar grupo', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'Compactos urbanos', exact: true })).toBeVisible();

  for (const modalidade of ['Diário', 'Controlado', 'Livre']) {
    await tabAte(page, page.getByRole('button', { name: `Configurar plano ${modalidade} de Compactos urbanos`, exact: true }));
    await page.keyboard.press('Enter');
    await expect(page.getByLabel('Preço por dia', { exact: true })).toBeFocused();
    await expect(dialogo.locator('input')).toHaveCount(modalidade === 'Diário' ? 2 : modalidade === 'Controlado' ? 3 : 1);
    if (modalidade === 'Livre') {
      await page.keyboard.type('-1');
      await tabAte(page, dialogo.getByRole('button', { name: 'Salvar plano', exact: true }));
      await page.keyboard.press('Enter');
      await expect(page.locator('#erro-editor-grupos')).toBeFocused();
      await tabAte(page, page.getByLabel('Preço por dia', { exact: true }));
      await page.keyboard.press('ControlOrMeta+A');
    }
    await page.keyboard.type(modalidade === 'Diário' ? '118,123456' : modalidade === 'Controlado' ? '154' : '229');
    if (modalidade === 'Diário') {
      await page.keyboard.press('Tab');
      await expect(page.getByLabel('Preço por quilômetro', { exact: true })).toBeFocused();
      await page.keyboard.type('1,25');
    } else if (modalidade === 'Controlado') {
      await page.keyboard.press('Tab');
      await expect(page.getByLabel('Franquia diária (km)')).toBeFocused();
      await page.keyboard.type('100');
      await page.keyboard.press('Tab');
      await expect(page.getByLabel('Preço por km excedente')).toBeFocused();
      await page.keyboard.type('1,05');
    }
    await tabAte(page, dialogo.getByRole('button', { name: 'Salvar plano', exact: true }));
    await page.keyboard.press('Enter');
    await expect(dialogo).not.toBeVisible();
    await expect(page.getByRole('status').filter({ hasText: 'Plano salvo' })).toBeVisible();
  }
  await expect(page.locator('#painel-grupos')).toContainText(/R\$\s*118,12/);
  await expect(page.locator('#painel-grupos')).toContainText('100 km/dia');

  await tabAte(page, page.getByRole('button', { name: 'Excluir grupo Compactos urbanos', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.locator('#erro-lista-grupos')).toBeFocused();
  await expect(page.locator('#erro-lista-grupos')).toContainText(/víncul|planos/i);
  await expect(page.getByRole('heading', { name: 'Compactos urbanos', exact: true })).toBeVisible();

  await tabAte(page, page.getByRole('tab', { name: 'Grupos', exact: true }));
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('tab', { name: 'Planos', exact: true })).toBeFocused();
  await expect(page.locator('#painel-planos')).toBeVisible();
  const livre = page.locator('#painel-planos article').filter({ has: page.getByRole('heading', { name: 'Livre', exact: true }) });
  const idAnterior = await livre.locator('.font-mono').innerText();
  await tabAte(page, page.getByRole('button', { name: 'Editar plano Diário', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByLabel('Preço por dia', { exact: true })).toHaveValue('118,123456');
  await page.keyboard.press('Escape');
  await expect(dialogo).not.toBeVisible();
  await expect(page.getByRole('button', { name: 'Editar plano Diário', exact: true })).toBeFocused();

  await tabAte(page, page.getByRole('button', { name: 'Excluir plano Livre', exact: true }));
  await page.keyboard.press('Enter');
  await expect(livre).toContainText('Não configurado');
  await tabAte(page, page.getByRole('button', { name: 'Configurar plano Livre', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByLabel('Preço por dia', { exact: true })).toBeFocused();
  await page.keyboard.type('230');
  await tabAte(page, dialogo.getByRole('button', { name: 'Salvar plano', exact: true }));
  await page.keyboard.press('Enter');
  await expect(dialogo).not.toBeVisible();
  await expect(livre).toContainText(/R\$\s*230,00/);
  expect(await livre.locator('.font-mono').innerText()).not.toBe(idAnterior);

  // O editor modal mantém Tab/Shift+Tab nos controles e restaura a origem.
  await tabAte(page, page.getByRole('button', { name: 'Editar plano Livre', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByLabel('Preço por dia', { exact: true })).toBeFocused();
  for (let indice = 0; indice < 8; indice++) {
    await page.keyboard.press(indice < 4 ? 'Tab' : 'Shift+Tab');
    expect(await dialogo.evaluate((elemento) => elemento.contains(document.activeElement))).toBe(true);
  }
  await page.keyboard.press('Escape');
  await expect(page.getByRole('button', { name: 'Editar plano Livre', exact: true })).toBeFocused();

  const contextoB = await browser.newContext();
  try {
    const paginaB = await contextoB.newPage();
    await entrarPorTeclado(paginaB, usuarioB, senha);
    await tabAte(paginaB, paginaB.getByRole('link', { name: 'Grupos e planos' }));
    await paginaB.keyboard.press('Enter');
    await expect(paginaB.getByRole('heading', { name: 'Nenhum grupo cadastrado' })).toBeVisible();
    await expect(paginaB.getByRole('heading', { name: 'Compactos urbanos', exact: true })).toHaveCount(0);
  } finally {
    await contextoB.close();
  }

  for (const modalidade of ['Diário', 'Controlado', 'Livre']) {
    await tabAte(page, page.getByRole('button', { name: `Excluir plano ${modalidade}`, exact: true }));
    await page.keyboard.press('Enter');
    await expect(page.getByRole('button', { name: `Configurar plano ${modalidade}`, exact: true })).toBeVisible();
  }
  await tabAte(page, page.getByRole('tab', { name: 'Planos', exact: true }));
  await page.keyboard.press('ArrowLeft');
  await tabAte(page, page.getByRole('button', { name: 'Excluir grupo Compactos urbanos', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'Nenhum grupo cadastrado' })).toBeVisible();
});

async function entrarPorTeclado(page: Page, usuario: string, senha: string): Promise<void> {
  await page.goto('/entrar');
  await tabAte(page, page.locator('#usuario'));
  await page.keyboard.type(usuario);
  await page.keyboard.press('Tab');
  await expect(page.locator('#senha')).toBeFocused();
  await page.keyboard.type(senha);
  await tabAte(page, page.getByRole('button', { name: 'Entrar', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/painel$/);
}

async function tabAte(page: Page, destino: Locator): Promise<void> {
  await expect(destino).toBeVisible();
  for (let indice = 0; indice < 70; indice++) {
    if (await destino.evaluate((elemento) => elemento === document.activeElement)) {
      await expect(destino).toBeFocused();
      return;
    }
    await page.keyboard.press('Tab');
  }
  throw new Error(`Destino não alcançado por Tab: ${destino}`);
}
