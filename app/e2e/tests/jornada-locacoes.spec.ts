import { randomUUID } from 'node:crypto';
import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test';

const api = 'http://127.0.0.1:5285/api/v1';
const senha = 'abcdefgh';

// Cadastros sintéticos pela API; a contratação, consulta e edição passam pela UI real.
async function preparar(request: APIRequestContext, letra: string) {
  const usuario = `f3${Date.now().toString(36)}${letra}${randomUUID().slice(0, 4)}`;
  expect((await request.post(`${api}/registro`, {
    data: { username: usuario, email: `${usuario}@example.invalid`, password: senha },
  })).status()).toBe(201);
  const login = await request.post(`${api}/session/login`, { data: { username: usuario, password: senha } });
  expect(login.status()).toBe(200);
  const headers = { Authorization: `Bearer ${(await login.json()).accessToken}` };
  async function criar(rota: string, data: unknown) {
    const resposta = await request.post(`${api}/${rota}`, { headers, data });
    expect(resposta.status(), `${rota}: ${await resposta.text()}`).toBe(201);
    return resposta.json();
  }
  const grupo = await criar('grupos', { nome: `Grupo sintético ${letra}` });
  const planos: Record<string, string> = {};
  for (const modalidade of ['Diario', 'Controlado', 'Livre']) {
    planos[modalidade] = (await criar('planos', {
      grupoId: grupo.id, modalidade, precoDiario: '100.00',
      precoKm: modalidade === 'Diario' ? '2.00' : null,
      franquiaDiariaKm: modalidade === 'Controlado' ? 100 : null,
      precoKmExcedente: modalidade === 'Controlado' ? '2.00' : null,
    })).id;
  }
  const veiculos = [];
  for (const placa of ['FIC1A01', 'FIC2A02', 'FIC3A03']) {
    veiculos.push(await criar('veiculos', {
      placa, marca: 'Marca sintética', modelo: `Modelo ${letra}`, cor: 'Azul',
      combustivel: 'Gasolina', capacidadeLitros: '50.000', ano: 2026, grupoId: grupo.id,
    }));
  }
  const cliente = await criar('clientes', {
    tipo: 'PF', nome: `Cliente sintético ${letra}`, cpf: '52998224725', rg: 'RG-sintetico',
    cnh: 'CNH-sintetica-418', telefone: '11900001842', email: null,
    endereco: { estado: 'SP', cidade: 'São Paulo', bairro: 'Centro', rua: 'Rua sintética', numero: '10' },
  });
  const condutor = await criar('condutores', {
    clienteId: cliente.id, proprioCliente: true, nome: cliente.nome, cpf: '52998224725',
    cnh: 'CNH-sintetica-418', validadeCnh: '2035-12-31', telefone: '11900001842',
    email: 'condutor@example.invalid',
  });
  const fixa = await criar('taxas-servicos', { nome: 'Entrega sintética', preco: '30.00', modalidade: 'Fixa' });
  const diaria = await criar('taxas-servicos', { nome: 'Cadeira sintética', preco: '10.00', modalidade: 'Diaria' });
  const nova = await criar('taxas-servicos', { nome: 'Adicional sintético novo', preco: '15.00', modalidade: 'Fixa' });
  for (const [tipo, precoDiario] of [['Cliente', '5.00'], ['Terceiros', '7.00']]) {
    expect((await request.put(`${api}/configuracoes/seguros/${tipo}`, { headers, data: { precoDiario } })).status()).toBe(201);
  }
  const retorno = new Date(Date.now() + 47 * 3_600_000);
  const partes = new Intl.DateTimeFormat('sv-SE', {
    timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
  }).format(retorno).replace(' ', 'T');
  const comando = {
    clienteId: cliente.id, condutorId: condutor.id, veiculoId: veiculos[0].id,
    planoId: planos.Controlado, retornoPrevisto: `${partes}:00-03:00`, hodometroInicial: 18420,
    taxasServicoIds: [fixa.id, diaria.id], seguros: ['Cliente', 'Terceiros'],
  };
  return { usuario, headers, grupo, planos, veiculos, cliente, condutor, fixa, diaria, nova, comando, retornoLocal: partes };
}

async function entrar(page: Page, usuario: string) {
  await page.goto('/entrar');
  await page.getByLabel('Usuário', { exact: true }).fill(usuario);
  await page.getByLabel('Senha', { exact: true }).fill(senha);
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page).toHaveURL(/\/painel$/);
  await page.getByRole('link', { name: 'Aluguéis', exact: true }).click();
}

async function teclado(alvo: Locator, tecla = 'Enter') {
  // A consulta inicial pode substituir o botão vazio pelo da lista carregada.
  // Reobtém o alvo antes da tecla, sem repetir o comando de negócio.
  await expect(async () => {
    await alvo.focus();
    await expect(alvo).toBeFocused({ timeout: 1000 });
  }).toPass({ timeout: 15000 });
  await alvo.press(tecla);
}

async function preencher(page: Page, dados: Awaited<ReturnType<typeof preparar>>) {
  await teclado(page.getByRole('button', { name: /Nova locação/ }).first());
  await expect(page.getByRole('heading', { name: 'Nova locação', exact: true })).toBeFocused();
  const cliente = page.getByLabel('Cliente contratante', { exact: true });
  await cliente.focus();
  await cliente.press('ArrowDown');
  await cliente.press('Tab');
  await expect(cliente).toHaveValue(dados.cliente.id);
  const condutor = page.getByLabel('Condutor', { exact: true });
  await expect(condutor.locator('option')).toHaveCount(2);
  await condutor.focus();
  await condutor.press('ArrowDown');
  await condutor.press('Tab');
  const veiculo = page.getByLabel('Veículo disponível', { exact: true });
  await veiculo.focus();
  await veiculo.press('ArrowDown');
  await veiculo.press('Tab');
  await teclado(page.getByRole('radio', { name: /Controlado/ }), 'Space');
  await page.getByLabel('Retorno previsto', { exact: true }).fill(dados.retornoLocal);
  await page.getByLabel('Hodômetro inicial (km)', { exact: true }).fill('18420');
  for (const nome of ['Entrega sintética', 'Cadeira sintética', 'Seguro do cliente', 'Seguro de terceiros']) {
    await teclado(page.getByRole('checkbox', { name: new RegExp(nome) }), 'Space');
  }
  await teclado(page.getByRole('button', { name: 'Calcular estimativa', exact: true }));
  await expect(page.getByLabel('Resumo da estimativa')).toContainText('274,00');
}

async function alterar(request: APIRequestContext, headers: Record<string, string>, rota: string, data: unknown) {
  const atual = await request.get(`${api}/${rota}`, { headers });
  expect(atual.status()).toBe(200);
  return request.put(`${api}/${rota}`, { headers: { ...headers, 'If-Match': atual.headers()['etag'] }, data });
}

async function verificarReflowEContraste(page: Page, largura: number) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  const contraste = await page.getByRole('button', { name: 'Confirmar locação', exact: true }).evaluate((botao) => {
    const estilo = getComputedStyle(botao);
    const contexto = document.createElement('canvas').getContext('2d')!;
    const luminancia = (cor: string) => {
      contexto.clearRect(0, 0, 1, 1);
      contexto.fillStyle = cor;
      contexto.fillRect(0, 0, 1, 1);
      const rgb = [...contexto.getImageData(0, 0, 1, 1).data].slice(0, 3).map(v => {
        const canal = v / 255;
        return canal <= 0.04045 ? canal / 12.92 : ((canal + 0.055) / 1.055) ** 2.4;
      });
      return rgb[0] * 0.2126 + rgb[1] * 0.7152 + rgb[2] * 0.0722;
    };
    const frente = luminancia(estilo.color);
    const fundo = luminancia(estilo.backgroundColor);
    return (Math.max(frente, fundo) + 0.05) / (Math.min(frente, fundo) + 0.05);
  });
  expect(contraste).toBeGreaterThanOrEqual(4.5);
  await page.screenshot({ path: `/tmp/opencode/f3-formulario-${largura}.png`, fullPage: true });
  if (largura === 1440) {
    // Metade da largura CSS equivale ao espaço de layout de desktop a 200%;
    // não é ensaio de zoom nativo de todos os navegadores.
    await page.setViewportSize({ width: 720, height: 900 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await expect(page.getByRole('button', { name: 'Confirmar locação', exact: true })).toBeEnabled();
    await page.screenshot({ path: '/tmp/opencode/f3-reflow-200.png', fullPage: true });
    await page.setViewportSize({ width: largura, height: 900 });
  }
}

for (const largura of [1440, 390]) {
  test(`J-03 real A/B: contratar, preservar preços, revisar e consultar por teclado em ${largura}px`, async ({ page, request }) => {
    test.setTimeout(180_000);
    await page.setViewportSize({ width: largura, height: 900 });
    const a = await preparar(request, 'A');
    const b = await preparar(request, 'B');
    await entrar(page, a.usuario);
    await preencher(page, a);
    await expect(page).toHaveURL(/\/alugueis\/nova$/);
    await verificarReflowEContraste(page, largura);
    expect((await request.get(`${api}/alugueis`, { headers: a.headers }).then(r => r.json())).total).toBe(0);
    const respostaCriacao = page.waitForResponse(r => r.request().method() === 'POST' && r.url() === `${api}/alugueis`);
    await teclado(page.getByRole('button', { name: 'Confirmar locação', exact: true }));
    const criado = await (await respostaCriacao).json();
    await expect(page.locator('#aviso-alugueis')).toContainText('Locação confirmada');
    await expect(page.locator('tbody tr')).toHaveCount(1);
    expect(criado.estimativa.totalConhecido).toBe('274.00');

    // A mesma documentação/placa pode existir em B, mas IDs e contratos não atravessam o tenant.
    expect((await request.get(`${api}/alugueis/${criado.id}`, { headers: b.headers })).status()).toBe(404);
    expect((await request.post(`${api}/alugueis/estimativas`, { headers: b.headers, data: a.comando })).status()).toBe(404);
    expect((await request.get(`${api}/alugueis`, { headers: b.headers }).then(r => r.json())).total).toBe(0);
    expect((await request.post(`${api}/alugueis`, { headers: { ...a.headers, 'Idempotency-Key': randomUUID() }, data: a.comando })).status()).toBe(409);
    for (const rota of [`veiculos/${a.veiculos[0].id}`, `taxas-servicos/${a.fixa.id}`, `planos/${a.planos.Controlado}`, `condutores/${a.condutor.id}`]) {
      const atual = await request.get(`${api}/${rota}`, { headers: a.headers });
      expect((await request.delete(`${api}/${rota}`, { headers: { ...a.headers, 'If-Match': atual.headers()['etag'] } })).status()).toBe(409);
    }
    expect((await alterar(request, a.headers, `planos/${a.planos.Controlado}`, {
      precoDiario: '150.00', precoKm: null, franquiaDiariaKm: 100, precoKmExcedente: '3.00',
    })).status()).toBe(200);
    expect((await alterar(request, a.headers, 'configuracoes/seguros/Cliente', { precoDiario: '9.00' })).status()).toBe(200);
    expect((await alterar(request, a.headers, 'configuracoes/seguros/Terceiros', { precoDiario: '11.00' })).status()).toBe(200);

    const consulta = page.getByRole('button', { name: `Consultar contrato ${criado.id}`, exact: true });
    await teclado(consulta);
    const dialogo = page.getByRole('dialog');
    await expect(dialogo).toContainText('274,00');
    await expect(dialogo).toContainText('200 km');
    await expect(dialogo).toContainText('Cheio');
    await expect(dialogo).toContainText('31/12/2035');
    await expect(dialogo).toContainText('50,00 L');
    await expect(dialogo).not.toContainText('52998224725');
    await expect(dialogo).not.toContainText('CNH-sintetica-418');
    await page.screenshot({ path: `/tmp/opencode/f3-contrato-${largura}.png`, fullPage: true });
    await page.getByRole('button', { name: 'Fechar consulta', exact: true }).focus();
    await page.keyboard.press('Shift+Tab');
    await expect(dialogo.getByRole('button', { name: 'Editar contrato', exact: true })).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(consulta).toBeFocused();
    await teclado(page.getByRole('button', { name: `Editar ${criado.id}`, exact: true }));
    await expect(page.getByRole('heading', { name: 'Editar locação' })).toBeFocused();
    await expect(page).toHaveURL(new RegExp(`/alugueis/${criado.id}/editar$`));
    await expect(page.getByRole('radio', { name: /Controlado/ }).locator('..')).toContainText('100,00');
    await expect(page.getByRole('checkbox', { name: /Seguro do cliente/ }).locator('..')).toContainText('5,00');
    await teclado(page.getByRole('checkbox', { name: /Seguro de terceiros/ }), 'Space');
    await teclado(page.getByRole('checkbox', { name: /Adicional sintético novo/ }), 'Space');
    await teclado(page.getByRole('button', { name: 'Calcular estimativa', exact: true }));
    await expect(page.getByLabel('Resumo da estimativa')).toContainText('275,00');
    const respostaEdicao = page.waitForResponse(r => r.request().method() === 'PUT' && r.url() === `${api}/alugueis/${criado.id}`);
    await teclado(page.getByRole('button', { name: 'Salvar alterações', exact: true }));
    const editado = await (await respostaEdicao).json();
    expect(editado.saidaUtc).toBe(criado.saidaUtc);
    expect(editado.revisao).toBe(2);
    expect(editado.estimativa.totalConhecido).toBe('275.00');
    await teclado(page.getByRole('button', { name: `Editar ${criado.id}`, exact: true }));
    await teclado(page.getByRole('checkbox', { name: /Seguro de terceiros/ }), 'Space');
    await teclado(page.getByRole('button', { name: 'Calcular estimativa', exact: true }));
    await expect(page.getByLabel('Resumo da estimativa')).toContainText('297,00');
    await teclado(page.getByRole('button', { name: 'Salvar alterações', exact: true }));
    await expect(page.locator('#aviso-alugueis')).toContainText('revisão 3');
    await teclado(page.getByRole('button', { name: `Consultar contrato ${criado.id}`, exact: true }));
    await expect(dialogo).toContainText('Revisão 3');
    await expect(dialogo).toContainText('297,00');
    await page.keyboard.press('Escape');
    expect(await page.evaluate(() => ({ local: localStorage.length, sessao: sessionStorage.length }))).toEqual({ local: 0, sessao: 0 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.screenshot({ path: `/tmp/opencode/f3-j03-${largura}.png`, fullPage: true });
  });
}

test('resposta perdida após commit mantém intenção, congela teclado e verifica a mesma chave', async ({ page, request }) => {
  test.setTimeout(120_000);
  const dados = await preparar(request, 'I');
  await entrar(page, dados.usuario);
  await preencher(page, dados);
  const chaves: string[] = [];
  await page.route(`${api}/alugueis`, async (route) => {
    if (route.request().method() !== 'POST') return route.continue();
    chaves.push(route.request().headers()['idempotency-key']);
    if (chaves.length === 1) {
      const resposta = await route.fetch();
      expect(resposta.status()).toBe(201);
      return route.abort('connectionfailed');
    }
    return route.continue();
  });
  await teclado(page.getByRole('button', { name: 'Confirmar locação', exact: true }));
  await expect(page.getByText('A contratação pode ter sido registrada.', { exact: true })).toBeVisible();
  await expect(page.getByLabel('Cliente contratante', { exact: true })).toBeDisabled();
  await teclado(page.getByRole('link', { name: 'Veículos', exact: true }));
  await expect(page).toHaveURL(/\/alugueis\/nova$/);
  await teclado(page.getByRole('button', { name: 'Verificar contratação', exact: true }));
  await expect(page.locator('#aviso-alugueis')).toContainText('Locação confirmada');
  expect(chaves).toHaveLength(2);
  expect(chaves[1]).toBe(chaves[0]);
  expect((await request.get(`${api}/alugueis`, { headers: dados.headers }).then(r => r.json())).total).toBe(1);
});

test('falha de cálculo permite retry e erro de campo recebe foco e associação', async ({ page, request }) => {
  test.setTimeout(120_000);
  const dados = await preparar(request, 'E');
  await entrar(page, dados.usuario);
  await preencher(page, dados);
  await page.getByLabel('Retorno previsto', { exact: true }).fill('2020-01-01T10:00');
  await teclado(page.getByRole('button', { name: 'Calcular estimativa', exact: true }));
  await expect(page.locator('#erro-contratacao')).toBeFocused();
  await expect(page.getByLabel('Retorno previsto', { exact: true })).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByLabel('Retorno previsto', { exact: true })).toHaveAttribute('aria-describedby', /erro-retorno/);
  await page.getByLabel('Retorno previsto', { exact: true }).fill(dados.retornoLocal);
  await page.route(`${api}/alugueis/estimativas`, async route => {
    await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ title: 'Dependência indisponível', status: 503 }) });
    await page.unroute(`${api}/alugueis/estimativas`);
  });
  await teclado(page.getByRole('button', { name: 'Calcular estimativa', exact: true }));
  await expect(page.getByRole('alert')).toContainText('Não foi possível calcular a estimativa');
  await expect(page.getByRole('button', { name: 'Confirmar locação', exact: true })).toBeDisabled();
  await teclado(page.getByRole('button', { name: 'Calcular estimativa', exact: true }));
  await expect(page.getByLabel('Resumo da estimativa')).toContainText('274,00');
});

test('401 na confirmação mantém tentativa e reautentica o mesmo ator antes do replay', async ({ page, request }) => {
  test.setTimeout(120_000);
  const dados = await preparar(request, 'R');
  await entrar(page, dados.usuario);
  await preencher(page, dados);
  let chave = '';
  await page.route(`${api}/alugueis`, async route => {
    if (route.request().method() !== 'POST') return route.continue();
    chave = route.request().headers()['idempotency-key'];
    const resposta = await route.fetch();
    expect(resposta.status()).toBe(201);
    await route.fulfill({ status: 401, body: '' });
    await page.unroute(`${api}/alugueis`);
  });
  await teclado(page.getByRole('button', { name: 'Confirmar locação', exact: true }));
  await expect(page.getByRole('button', { name: 'Reautenticar', exact: true })).toBeVisible();
  await page.getByLabel('Usuário', { exact: true }).fill(dados.usuario);
  await page.getByLabel('Senha', { exact: true }).fill(senha);
  await teclado(page.getByRole('button', { name: 'Reautenticar', exact: true }));
  await expect(page.getByRole('button', { name: 'Reautenticar', exact: true })).toHaveCount(0);
  const replay = page.waitForRequest(r => r.method() === 'POST' && r.url() === `${api}/alugueis`);
  await teclado(page.getByRole('button', { name: 'Verificar contratação', exact: true }));
  expect((await replay).headers()['idempotency-key']).toBe(chave);
  await expect(page.locator('#aviso-alugueis')).toContainText('Locação confirmada');
  expect((await request.get(`${api}/alugueis`, { headers: dados.headers }).then(r => r.json())).total).toBe(1);
});

test('edição concorrente exige recarga explícita sem repetir PUT ou perder saída', async ({ page, request }) => {
  test.setTimeout(120_000);
  const dados = await preparar(request, 'C');
  const resposta = await request.post(`${api}/alugueis`, {
    headers: { ...dados.headers, 'Idempotency-Key': randomUUID() }, data: dados.comando,
  });
  expect(resposta.status()).toBe(201);
  const criado = await resposta.json();
  await entrar(page, dados.usuario);
  await teclado(page.getByRole('button', { name: `Editar ${criado.id}`, exact: true }));
  await expect(page.getByRole('heading', { name: 'Editar locação' })).toBeVisible();
  expect((await alterar(request, dados.headers, `alugueis/${criado.id}`, { ...dados.comando, hodometroInicial: 19000 })).status()).toBe(200);
  let envios = 0;
  page.on('request', r => { if (r.method() === 'PUT' && r.url() === `${api}/alugueis/${criado.id}`) envios++; });
  await teclado(page.getByRole('button', { name: 'Salvar alterações', exact: true }));
  await expect(page.getByRole('alert')).toContainText('atualizado por outra operação');
  await expect(page.getByRole('button', { name: 'Salvar alterações', exact: true })).toBeDisabled();
  expect(envios).toBe(1);
  await teclado(page.getByRole('button', { name: 'Recarregar contrato', exact: true }));
  await expect(page.getByLabel('Hodômetro inicial (km)', { exact: true })).toHaveValue('19000');
  await expect(page.getByRole('button', { name: 'Salvar alterações', exact: true })).toBeEnabled();
});
