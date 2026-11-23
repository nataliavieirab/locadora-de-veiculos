import { expect, test, type Page } from '@playwright/test';

test('J-02 integrada: duas empresas preparam todos os cadastros sem foto ou aluguel', async ({ browser, request }) => {
  test.setTimeout(240_000);
  const sufixo = Date.now().toString(36);
  const contextos = [await browser.newContext(), await browser.newContext()];
  try {
    const paginas = [await contextos[0].newPage(), await contextos[1].newPage()];
    for (let indice = 0; indice < 2; indice++) {
      const usuario = `jornada${sufixo}${indice}`;
      const registro = await request.post('http://127.0.0.1:5285/api/v1/registro', {
        data: { username: usuario, email: `${usuario}@example.invalid`, password: 'abcdefgh' },
      });
      expect(registro.status()).toBe(201);
      await prepararEmpresa(paginas[indice], usuario, indice === 0 ? 'A' : 'B');
    }
    for (let indice = 0; indice < 2; indice++) {
      const page = paginas[indice];
      const propria = indice === 0 ? 'A' : 'B';
      const externa = indice === 0 ? 'B' : 'A';
      await navegar(page, 'Clientes e condutores');
      await expect(page.locator('#painel-clientes tbody tr')).toHaveCount(2);
      await expect(page.locator('#painel-clientes')).toContainText(`Cliente PF ${propria}`);
      await expect(page.locator('#painel-clientes')).not.toContainText(`Cliente PF ${externa}`);
      await page.getByRole('tab', { name: 'Condutores', exact: true }).click();
      await expect(page.locator('#painel-condutores tbody tr')).toHaveCount(1);
      await expect(page.locator('#painel-condutores')).toContainText(`Cliente PF ${propria}`);
      await navegar(page, 'Grupos e planos');
      await expect(page.locator('#painel-grupos')).toContainText(`Grupo ${propria}`);
      await expect(page.locator('#painel-grupos')).not.toContainText(`Grupo ${externa}`);
      await navegar(page, 'Veículos');
      await expect(page.getByRole('row').filter({ hasText: 'ABC-1D23' })).toHaveCount(1);
      await expect(page.getByRole('row').filter({ hasText: 'ABC-1D23' })).toContainText(`Modelo ${propria}`);
      await navegar(page, 'Preços e serviços');
      await expect(page.locator('#painel-taxas tbody tr')).toHaveCount(1);
      await expect(page.locator('#painel-taxas')).toContainText(`Taxa ${propria}`);
      await expect(page.locator('#painel-taxas')).not.toContainText(`Taxa ${externa}`);
      await page.getByRole('tab', { name: 'Seguros', exact: true }).click();
      await expect(page.getByRole('article', { name: 'Seguro do cliente', exact: true })).toContainText(indice === 0 ? '5,00' : '9,00');
      await page.getByRole('tab', { name: 'Combustíveis', exact: true }).click();
      await expect(page.getByRole('article', { name: 'Gasolina', exact: true })).toContainText(indice === 0 ? '6,00' : '8,00');
      expect(await page.evaluate(() => ({ local: localStorage.length, sessao: sessionStorage.length }))).toEqual({ local: 0, sessao: 0 });
    }
  } finally {
    for (const contexto of contextos) await contexto.close();
  }
});

async function prepararEmpresa(page: Page, usuario: string, letra: string): Promise<void> {
  await page.goto('http://localhost:4200/entrar');
  await page.locator('#usuario').fill(usuario);
  await page.locator('#senha').fill('abcdefgh');
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page).toHaveURL(/\/painel$/);
  await page.getByRole('link', { name: 'Clientes e condutores', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Nenhum cliente cadastrado' })).toBeVisible();
  for (const tipo of ['PF', 'PJ']) {
    await page.getByRole('button', { name: '＋ Novo cliente', exact: true }).click();
    if (tipo === 'PJ') await page.getByRole('button', { name: 'Pessoa jurídica', exact: true }).click();
    await preencher(page, {
      'nome-cliente': `Cliente ${tipo} ${letra}`, 'documento-cliente': tipo === 'PF' ? '52998224725' : '11222333000181',
      'telefone-cliente': '(11) 90000-1842', 'estado-cliente': 'SP', 'cidade-cliente': 'São Paulo',
      'bairro-cliente': 'Centro', 'rua-cliente': 'Rua sintética', 'numero-cliente': '10',
      ...(tipo === 'PF' ? { 'rg-cliente': 'RG-sintetico', 'cnh-cliente': 'CNH-418' } : {}),
    });
    await salvar(page, 'Salvar cliente');
  }
  await page.getByRole('tab', { name: 'Condutores', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Nenhum condutor cadastrado' })).toBeVisible();
  await page.getByRole('button', { name: '＋ Novo condutor', exact: true }).click();
  await page.locator('#cliente-condutor').selectOption({ label: `Cliente PF ${letra} — PF` });
  await page.locator('#proprio-condutor').check();
  await expect(page.locator('#cpf-condutor')).toHaveValue('52998224725');
  await expect(page.locator('#cnh-condutor')).toHaveValue('CNH-418');
  await preencher(page, { 'validade-condutor': '2028-09-18', 'email-condutor': `condutor${letra.toLowerCase()}@example.invalid` });
  await salvar(page, 'Salvar condutor');
  await page.getByRole('tab', { name: 'Vínculos PF–PJ', exact: true }).click();
  await page.getByRole('button', { name: 'Adicionar vínculo', exact: true }).click();
  await expect(page.getByRole('button', { name: `Remover vínculo de Cliente PF ${letra}`, exact: true })).toBeVisible();

  await navegar(page, 'Grupos e planos');
  await expect(page.getByRole('heading', { name: 'Nenhum grupo cadastrado' })).toBeVisible();
  await page.getByRole('button', { name: '＋ Novo grupo', exact: true }).click();
  await page.getByLabel('Nome do grupo', { exact: true }).fill(`Grupo ${letra}`);
  await salvar(page, 'Salvar grupo');
  for (const modalidade of ['Diário', 'Controlado', 'Livre']) {
    await page.getByRole('button', { name: `Configurar plano ${modalidade} de Grupo ${letra}`, exact: true }).click();
    await page.locator('#preco-diario').fill('100');
    if (modalidade === 'Diário') await page.locator('#preco-km').fill('1,25');
    if (modalidade === 'Controlado') await preencher(page, { 'franquia-diaria': '100', 'preco-excedente': '2' });
    await salvar(page, 'Salvar plano');
  }
  await navegar(page, 'Veículos');
  await expect(page.getByRole('heading', { name: 'Nenhum veículo para exibir' })).toBeVisible();
  await page.getByRole('button', { name: '＋ Novo veículo', exact: true }).click();
  expect(await page.getByRole('dialog').locator('input[type=file]').count()).toBe(0);
  await preencher(page, {
    'placa-veiculo': 'ABC-1D23', 'marca-veiculo': 'Marca sintética', 'modelo-veiculo': `Modelo ${letra}`,
    'cor-veiculo': 'Azul', 'ano-veiculo': '2026', 'capacidade-veiculo': '48,125',
  });
  await page.locator('#grupo-veiculo').selectOption({ label: `Grupo ${letra}` });
  await salvar(page, 'Salvar veículo');
  await navegar(page, 'Preços e serviços');
  await expect(page.getByRole('heading', { name: 'Nenhuma taxa ou serviço cadastrado' })).toBeVisible();
  await page.getByRole('button', { name: '＋ Nova taxa ou serviço', exact: true }).click();
  await preencher(page, { 'nome-taxa': `Taxa ${letra}`, 'preco-taxa': '15' });
  await salvar(page, 'Salvar taxa ou serviço');
  await page.getByRole('tab', { name: 'Seguros', exact: true }).click();
  await expect(page.getByRole('article', { name: 'Seguro do cliente', exact: true })).toContainText('Não configurado');
  await preencher(page, { 'seguro-Cliente': letra === 'A' ? '5' : '9', 'seguro-Terceiros': '7' });
  await page.getByRole('button', { name: 'Salvar seguros', exact: true }).click();
  await expect(page.getByRole('article', { name: 'Seguro de terceiros', exact: true })).toContainText('7,00');
  await page.getByRole('tab', { name: 'Combustíveis', exact: true }).click();
  for (const nome of ['Gasolina', 'Gás', 'Diesel', 'Álcool']) {
    await expect(page.getByRole('article', { name: nome, exact: true })).toContainText('Não configurado');
    await page.getByRole('button', { name: `Definir primeiro preço · ${nome}`, exact: true }).click();
    await page.locator('#preco-combustivel').fill(letra === 'A' ? '6' : '8');
    await salvar(page, 'Criar vigência');
    await expect(page.getByRole('article', { name: nome, exact: true }).getByRole('listitem')).toHaveCount(1);
  }
}

async function navegar(page: Page, nome: string): Promise<void> {
  await page.getByRole('navigation', { name: 'Navegação cadastral' }).getByRole('link', { name: nome, exact: true }).click();
  await expect(page.getByRole('heading', { name: nome, exact: true })).toBeVisible();
}

async function preencher(page: Page, dados: Record<string, string>): Promise<void> {
  for (const [id, valor] of Object.entries(dados)) await page.locator(`#${id}`).fill(valor);
}

async function salvar(page: Page, nome: string): Promise<void> {
  await page.getByRole('dialog').getByRole('button', { name: nome, exact: true }).click();
  await expect(page.getByRole('dialog')).not.toBeVisible();
}
