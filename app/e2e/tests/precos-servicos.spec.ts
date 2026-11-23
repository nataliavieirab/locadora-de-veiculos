import { expect, test, type Locator, type Page } from '@playwright/test';

test('preços: taxas fixa/diária, seguros independentes e histórico de combustível por teclado', async ({ page, request }) => {
  test.setTimeout(180_000);
  const usuario = `precos${Date.now().toString(36)}`;
  const registro = await request.post('http://127.0.0.1:5285/api/v1/registro', {
    data: { username: usuario, email: `${usuario}@example.invalid`, password: 'abcdefgh' },
  });
  expect(registro.status()).toBe(201);
  await page.goto('/entrar');
  await tabAte(page, page.locator('#usuario'));
  await page.keyboard.type(usuario);
  await tabAte(page, page.locator('#senha'));
  await page.keyboard.type('abcdefgh');
  await tabAte(page, page.getByRole('button', { name: 'Entrar', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/painel$/);
  await tabAte(page, page.getByRole('link', { name: 'Preços e serviços', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'Nenhuma taxa ou serviço cadastrado' })).toBeVisible();
  const dialogo = page.getByRole('dialog');

  await novaTaxa('Limpeza sintética', '95', false);
  await novaTaxa('Assento sintético', '18,123456', true);
  await expect(page.getByRole('row').filter({ hasText: 'Assento sintético' })).toContainText('Diária');
  await expect(page.getByRole('row').filter({ hasText: 'Limpeza sintética' })).toContainText('Fixa');
  await tabAte(page, page.getByRole('button', { name: 'Editar Assento sintético', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByLabel('Nome', { exact: true })).toBeFocused();
  await expect(page.getByLabel('Preço', { exact: true })).toHaveValue('18,123456');
  await tabAte(page, page.getByLabel('Preço', { exact: true }));
  await page.keyboard.press('ControlOrMeta+A');
  await page.keyboard.type('20');
  await salvarTaxa();
  await expect(page.getByRole('row').filter({ hasText: 'Assento sintético' })).toContainText('20,00');

  await tabAte(page, page.getByRole('tab', { name: 'Taxas e serviços', exact: true }));
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('tab', { name: 'Seguros', exact: true })).toBeFocused();
  const cliente = page.getByRole('article', { name: 'Seguro do cliente', exact: true });
  const terceiros = page.getByRole('article', { name: 'Seguro de terceiros', exact: true });
  await expect(cliente).toContainText('Não configurado');
  await expect(terceiros).toContainText('Não configurado');
  await tabAte(page, page.locator('#seguro-Cliente'));
  await page.keyboard.type('0');
  await tabAte(page, page.locator('#seguro-Terceiros'));
  await page.keyboard.type('-1');
  await tabAte(page, page.getByRole('button', { name: 'Salvar seguros', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.locator('#erro-seguros')).toBeFocused();
  await expect(page.locator('#seguro-Terceiros')).toHaveAttribute('aria-invalid', 'true');
  await expect(cliente).toContainText('Não configurado');
  await tabAte(page, page.locator('#seguro-Terceiros'));
  await page.keyboard.press('ControlOrMeta+A');
  await page.keyboard.type('7,50');
  await tabAte(page, page.getByRole('button', { name: 'Salvar seguros', exact: true }));
  await page.keyboard.press('Enter');
  await expect(cliente).toContainText('0,00');
  await expect(terceiros).toContainText('7,50');
  await tabAte(page, page.locator('#seguro-Cliente'));
  await page.keyboard.press('ControlOrMeta+A');
  await page.keyboard.type('5');
  const alteracoes: string[] = [];
  page.on('request', (pedido) => {
    if (pedido.method() === 'PUT' && pedido.url().includes('/configuracoes/seguros/')) alteracoes.push(pedido.url());
  });
  await tabAte(page, page.getByRole('button', { name: 'Salvar seguros', exact: true }));
  await page.keyboard.press('Enter');
  await expect(cliente).toContainText('5,00');
  await expect(terceiros).toContainText('7,50');
  expect(alteracoes).toEqual(['http://127.0.0.1:5285/api/v1/configuracoes/seguros/Cliente']);

  await tabAte(page, page.getByRole('tab', { name: 'Seguros', exact: true }));
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('tab', { name: 'Combustíveis', exact: true })).toBeFocused();
  for (const nome of ['Gasolina', 'Gás', 'Diesel', 'Álcool']) {
    await expect(page.getByRole('article', { name: nome, exact: true })).toContainText('Não configurado');
  }
  await criarVigencia('Definir primeiro preço · Gasolina', '6,123456', true);
  const gasolina = page.getByRole('article', { name: 'Gasolina', exact: true });
  await expect(gasolina.getByRole('listitem')).toHaveCount(1);
  await expect(gasolina.getByRole('listitem')).toContainText('UTC');
  await criarVigencia('Definir novo preço · Gasolina', '7', false);
  await expect(gasolina.getByRole('listitem')).toHaveCount(2);
  await expect(gasolina.getByRole('listitem').nth(0)).toContainText('7,00');
  await expect(gasolina.getByRole('listitem').nth(1)).toContainText('6,12');
  for (const nome of ['Gás', 'Diesel', 'Álcool']) {
    await criarVigencia(`Definir primeiro preço · ${nome}`, '0', false);
    await expect(page.getByRole('article', { name: nome, exact: true })).toContainText('0,00');
  }
  await tabAte(page, page.getByRole('tab', { name: 'Combustíveis', exact: true }));
  await page.keyboard.press('Home');
  await expect(page.getByRole('tab', { name: 'Taxas e serviços', exact: true })).toBeFocused();
  for (const nome of ['Assento sintético', 'Limpeza sintética']) {
    await tabAte(page, page.getByRole('button', { name: `Excluir ${nome}`, exact: true }));
    await page.keyboard.press('Enter');
    await expect(page.getByRole('row').filter({ hasText: nome })).toHaveCount(0);
  }
  await expect(page.getByRole('heading', { name: 'Nenhuma taxa ou serviço cadastrado' })).toBeVisible();

  async function novaTaxa(nome: string, preco: string, diaria: boolean): Promise<void> {
    await tabAte(page, page.getByRole('button', { name: '＋ Nova taxa ou serviço', exact: true }));
    await page.keyboard.press('Enter');
    await expect(page.getByLabel('Nome', { exact: true })).toBeFocused();
    await page.keyboard.type(nome);
    await tabAte(page, page.getByLabel('Preço', { exact: true }));
    await page.keyboard.type('-1');
    await tabAte(page, dialogo.getByRole('button', { name: 'Salvar taxa ou serviço', exact: true }));
    await page.keyboard.press('Enter');
    await expect(page.locator('#erro-editor-precos')).toBeFocused();
    await expect(page.getByLabel('Preço', { exact: true })).toHaveAttribute('aria-invalid', 'true');
    await tabAte(page, page.getByLabel('Preço', { exact: true }));
    await page.keyboard.press('ControlOrMeta+A');
    await page.keyboard.type(preco);
    await tabAte(page, page.getByLabel('Modalidade', { exact: true }));
    await page.keyboard.press(diaria ? 'End' : 'Home');
    await salvarTaxa();
    await expect(page.getByRole('row').filter({ hasText: nome })).toBeVisible();
  }

  async function salvarTaxa(): Promise<void> {
    await tabAte(page, dialogo.getByRole('button', { name: 'Salvar taxa ou serviço', exact: true }));
    await page.keyboard.press('Enter');
    await expect(dialogo).not.toBeVisible();
  }

  async function criarVigencia(acao: string, preco: string, testarNegativo: boolean): Promise<void> {
    await tabAte(page, page.getByRole('button', { name: acao, exact: true }));
    await page.keyboard.press('Enter');
    await expect(page.getByLabel('Preço por litro', { exact: true })).toBeFocused();
    if (testarNegativo) {
      await page.keyboard.type('-1');
      await tabAte(page, dialogo.getByRole('button', { name: 'Criar vigência', exact: true }));
      await page.keyboard.press('Enter');
      await expect(page.locator('#erro-editor-precos')).toBeFocused();
      await tabAte(page, page.getByLabel('Preço por litro', { exact: true }));
      await page.keyboard.press('ControlOrMeta+A');
    }
    await page.keyboard.type(preco);
    await tabAte(page, dialogo.getByRole('button', { name: 'Criar vigência', exact: true }));
    await page.keyboard.press('Enter');
    await expect(dialogo).not.toBeVisible();
  }
});

async function tabAte(page: Page, destino: Locator): Promise<void> {
  await expect(destino).toBeVisible();
  for (let indice = 0; indice < 100; indice++) {
    if (await destino.evaluate((elemento) => elemento === document.activeElement)) return;
    await page.keyboard.press('Tab');
  }
  throw new Error(`Destino não alcançado por Tab: ${destino}`);
}
