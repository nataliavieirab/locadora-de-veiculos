import path from 'node:path';
import { expect, test, type Page, type APIRequestContext, type Locator } from '@playwright/test';

const api = 'http://127.0.0.1:5285/api/v1';

async function preparar(page: Page, request: APIRequestContext) {
  const usuario = `foto${Date.now().toString(36)}${Math.random().toString(36).slice(2, 7)}`;
  const password = 'abcdefgh';
  expect((await request.post(`${api}/registro`, { data: { username: usuario, email: `${usuario}@example.invalid`, password } })).status()).toBe(201);
  await page.goto('/entrar');
  await page.getByLabel('Usuário', { exact: true }).fill(usuario);
  await page.getByLabel('Senha', { exact: true }).fill(password);
  const login = page.waitForResponse(r => r.url().endsWith('/session/login'));
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  const headers = { Authorization: `Bearer ${(await (await login).json()).accessToken}` };
  const grupo = await request.post(`${api}/grupos`, { headers, data: { nome: 'Econômico sintético' } });
  const grupoId = (await grupo.json()).id;
  const criado = await request.post(`${api}/veiculos`, { headers, data: { placa: 'FIC1A01', marca: 'Marca fictícia',
    modelo: 'Horizonte', cor: 'Prata', ano: 2026, combustivel: 'Gasolina', capacidadeLitros: '50', grupoId } });
  expect(criado.status()).toBe(201);
  const id = (await criado.json()).id;
  await page.getByRole('link', { name: 'Veículos', exact: true }).click();
  await page.getByRole('button', { name: 'Editar veículo FIC1A01', exact: true }).click();
  await expect(page.locator('#selecionar-foto')).toBeEnabled();
  const base64 = await page.evaluate(() => {
    const canvas = document.createElement('canvas'); canvas.width = 320; canvas.height = 200;
    const ctx = canvas.getContext('2d')!; ctx.fillStyle = '#d6d3d1'; ctx.fillRect(0, 0, 320, 200);
    ctx.fillStyle = '#1e3a8a'; ctx.fillRect(40, 80, 240, 80);
    return canvas.toDataURL('image/png').split(',')[1];
  });
  const arquivo = { name: 'sintetica.png', mimeType: 'image/png', buffer: Buffer.from(base64, 'base64') };
  return { id, headers, arquivo };
}

async function tabAte(page: Page, destino: Locator) {
  for (let i = 0; i < 60; i++) {
    if (await destino.evaluate(el => document.activeElement === el)) return;
    await page.keyboard.press('Tab');
  }
  throw new Error('Controle não alcançado por teclado');
}

for (const [resultado, width] of [['sucesso', 1440], ['falha', 390], ['perdida', 720]] as const) {
  test(`cadastro com foto em uma ação: ${resultado} ${width}px`, async ({ page, request }, info) => {
    test.setTimeout(120_000);
    await page.setViewportSize({ width, height: 900 });
    const { arquivo, headers } = await preparar(page, request);
    await page.keyboard.press('Escape');
    await page.getByRole('button', { name: 'Novo veículo' }).click();
    const dialogo = page.getByRole('dialog');
    for (const [rotulo, valor] of [['Placa', 'NOV1A01'], ['Marca', 'Marca sintética'], ['Modelo', 'Novo'],
      ['Cor', 'Azul'], ['Ano', '2026'], ['Capacidade (litros)', '50']]) {
      await dialogo.getByLabel(rotulo, { exact: true }).fill(valor);
    }
    await dialogo.getByLabel('Grupo', { exact: true }).selectOption({ label: 'Econômico sintético' });
    expect(await page.locator('#capacidade-veiculo').evaluate(el =>
      !!(el.compareDocumentPosition(document.getElementById('arquivo-foto')!) & Node.DOCUMENT_POSITION_FOLLOWING))).toBe(true);
    await page.locator('#arquivo-foto').setInputFiles(arquivo);
    await expect(page.locator('#salvar-veiculo')).toBeFocused();
    await expect(page.locator('#enviar-foto')).toHaveCount(0);
    await expect(dialogo).toContainText('Prévia não enviada');
    await page.screenshot({ path: info.outputPath(`cadastro-previa-${width}.png`) });
    let criacoes = 0, uploads = 0;
    page.on('request', r => {
      if (r.method() === 'POST' && r.url() === `${api}/veiculos`) criacoes++;
      if (r.method() === 'PUT' && r.url().endsWith('/foto')) uploads++;
    });
    await page.route('**/api/v1/veiculos/*/foto', async route => {
      if (route.request().method() !== 'PUT') { await route.continue(); return; }
      if (resultado === 'falha') {
        await route.fulfill({ status: 503, contentType: 'application/problem+json',
          body: '{"code":"foto_indisponivel","title":"Armazenamento indisponível"}' });
      } else if (resultado === 'perdida') {
        expect((await route.fetch()).status()).toBe(200);
        await route.abort('failed');
      } else await route.continue();
    });
    const criada = page.waitForResponse(r => r.request().method() === 'POST' && r.url() === `${api}/veiculos`);
    await page.keyboard.press('Enter');
    const resposta = await criada;
    expect(resposta.status()).toBe(201);
    const id = (await resposta.json()).id;
    if (resultado === 'sucesso') {
      await expect(dialogo).toContainText('Veículo e foto salvos.');
      await expect(dialogo.getByAltText('Foto atual do veículo NOV1A01')).toBeVisible();
    } else {
      await expect(page.locator('#erro-editor-veiculos')).toBeFocused();
      await expect(dialogo).toContainText('Veículo salvo, mas');
      await expect(dialogo).not.toContainText('Veículo e foto salvos.');
      if (resultado === 'perdida') {
        await expect(dialogo).toContainText('Resultado não confirmado');
        await expect(page.locator('#salvar-veiculo')).toBeDisabled();
        await page.locator('#recarregar-foto').click();
        await expect(dialogo).toContainText('referência atual');
        await expect(dialogo).not.toContainText('Foto adicionada');
        await expect(page.locator('#erro-editor-veiculos')).not.toBeVisible();
      } else {
        const detalhe = await request.get(`${api}/veiculos/${id}`, { headers });
        expect(detalhe.status()).toBe(200);
        expect((await detalhe.json()).foto).toBeNull();
        await page.unroute('**/api/v1/veiculos/*/foto');
        await page.locator('#enviar-foto').click();
        await expect(dialogo).toContainText('Foto adicionada.');
        await expect(page.locator('#erro-editor-veiculos')).not.toBeVisible();
      }
    }
    expect(criacoes).toBe(1);
    expect(uploads).toBe(resultado === 'falha' ? 2 : 1);
    await page.locator('#selecionar-foto').scrollIntoViewIfNeeded();
    await page.screenshot({ path: info.outputPath(`cadastro-resultado-${width}.png`) });
    const detalhe = await request.get(`${api}/veiculos/${id}`, { headers });
    expect((await detalhe.json()).foto).not.toBeNull();
    await page.keyboard.press('Escape');
    await expect(dialogo).not.toBeVisible();
    await expect(page.getByRole('button', { name: 'Novo veículo' })).toBeFocused();
  });
}

test('gate CA-01/03: fotos locais reais isoladas A/B, anônimo, URL externa e sessão revogada', async ({ page, request }) => {
  test.setTimeout(120_000);
  const a = await preparar(page, request);
  const usuario = `gate${Date.now().toString(36)}${Math.random().toString(36).slice(2, 7)}`;
  expect((await request.post(`${api}/registro`, {
    data: { username: usuario, email: `${usuario}@example.invalid`, password: 'abcdefgh' },
  })).status()).toBe(201);
  const login = await request.post(`${api}/session/login`, { data: { username: usuario, password: 'abcdefgh' } });
  const headersB = { Authorization: `Bearer ${(await login.json()).accessToken}` };
  const grupo = await request.post(`${api}/grupos`, { headers: headersB, data: { nome: 'Grupo B sintético' } });
  const criado = await request.post(`${api}/veiculos`, { headers: headersB, data: {
    placa: 'FIC1A01', marca: 'Marca B', modelo: 'Outro', cor: 'Azul', ano: 2026,
    combustivel: 'Gasolina', capacidadeLitros: '50', grupoId: (await grupo.json()).id,
  } });
  expect(criado.status()).toBe(201);
  const b = { id: (await criado.json()).id as string, headers: headersB };
  for (const dono of [a, b]) {
    const detalhe = await request.get(`${api}/veiculos/${dono.id}`, { headers: dono.headers });
    const enviada = await request.put(`${api}/veiculos/${dono.id}/foto`, {
      headers: { ...dono.headers, 'If-Match': detalhe.headers()['etag'] }, multipart: { arquivo: a.arquivo },
    });
    expect(enviada.status()).toBe(200);
    const etag = enviada.headers()['etag'];
    const ativa = (await enviada.json()).foto;
    expect(ativa.caminho).toBe(`/api/v1/veiculos/${dono.id}/foto`);
    const leitura = await request.get(`${api}/veiculos/${dono.id}/foto`, { headers: dono.headers, maxRedirects: 0 });
    expect(leitura.status()).toBe(200);
    expect(leitura.headers()['location']).toBeUndefined();
    expect(leitura.headers()['cache-control']).toContain('no-store');
    expect((await leitura.body()).subarray(0, 8)).toEqual(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]));
    const outro = dono.id === a.id ? b : a;
    for (const headers of [outro.headers, {}]) {
      const esperado = 'Authorization' in headers ? 404 : 401;
      for (const sufixo of ['', '/foto']) {
        const negada = await request.get(`${api}/veiculos/${dono.id}${sufixo}`, { headers, maxRedirects: 0 });
        expect(negada.status()).toBe(esperado);
        expect(negada.headers()['location']).toBeUndefined();
        expect(await negada.text()).not.toContain(ativa.id);
      }
      expect((await request.put(`${api}/veiculos/${dono.id}/foto`, {
        headers: { ...headers, 'If-Match': etag }, multipart: { arquivo: a.arquivo },
      })).status()).toBe(esperado);
      expect((await request.delete(`${api}/veiculos/${dono.id}/foto`, {
        headers: { ...headers, 'If-Match': etag },
      })).status()).toBe(esperado);
    }
    // URL não é uma fonte suportada: campo extra e entrada JSON são rejeitados.
    expect((await request.put(`${api}/veiculos/${dono.id}/foto`, {
      headers: { ...dono.headers, 'If-Match': etag },
      multipart: { arquivo: a.arquivo, url: 'https://example.invalid/sintetica.png' },
    })).status()).toBe(400);
    expect((await request.put(`${api}/veiculos/${dono.id}/foto`, {
      headers: { ...dono.headers, 'If-Match': etag }, data: { url: 'https://example.invalid/sintetica.png' },
    })).status()).toBe(415);
    const preservada = await request.get(`${api}/veiculos/${dono.id}`, { headers: dono.headers });
    expect((await preservada.json()).foto.id).toBe(ativa.id);
    expect(preservada.headers()['etag']).toBe(etag);
  }
  expect((await request.post(`${api}/session/logout`, { headers: a.headers, data: {} })).status()).toBe(204);
  for (const sufixo of ['', '/foto']) {
    expect((await request.get(`${api}/veiculos/${a.id}${sufixo}`, { headers: a.headers })).status()).toBe(401);
  }
  expect((await request.put(`${api}/veiculos/${a.id}/foto`, {
    headers: a.headers, multipart: { arquivo: a.arquivo },
  })).status()).toBe(401);
  expect((await request.delete(`${api}/veiculos/${a.id}/foto`, { headers: a.headers })).status()).toBe(401);
  expect((await request.get(`${api}/veiculos/${b.id}/foto`, { headers: b.headers })).status()).toBe(200);
});

for (const width of [1440, 390, 720]) {
  test(`foto privada real e paridade do editor ${width}px`, async ({ page, request }, info) => {
    test.setTimeout(120_000);
    await page.setViewportSize({ width, height: 900 });
    const { arquivo } = await preparar(page, request);
    const dialogo = page.getByRole('dialog');
    await expect(dialogo).toContainText('Sem foto');
    expect(await dialogo.evaluate(el => el.scrollWidth <= el.clientWidth)).toBe(true);
    await tabAte(page, page.locator('#selecionar-foto'));
    await expect(page.locator('#selecionar-foto')).toBeFocused();
    const escolha = page.waitForEvent('filechooser');
    await page.keyboard.press('Enter');
    await (await escolha).setFiles(arquivo);
    await expect(page.locator('#enviar-foto')).toBeFocused();
    await expect(dialogo).toContainText('Prévia não enviada');
    await page.screenshot({ path: info.outputPath(`angular-previa-${width}.png`) });
    await page.keyboard.press('Enter');
    await expect(dialogo).toContainText('Foto adicionada.');
    await expect(dialogo.getByAltText('Foto atual do veículo FIC1A01')).toBeVisible();
    await expect(page.locator('#selecionar-foto')).toBeEnabled();
    await dialogo.locator('.overflow-y-auto').evaluate(el => { el.scrollTop = 0; });
    await page.screenshot({ path: info.outputPath(`angular-ativa-${width}.png`) });
    const imagem = dialogo.locator('img');
    expect(await imagem.getAttribute('src')).toMatch(/^blob:/);
    const cores = await page.locator('#selecionar-foto').evaluate(el => {
      const css = getComputedStyle(el);
      const canvas = document.createElement('canvas'); canvas.width = 1; canvas.height = 1;
      const ctx = canvas.getContext('2d')!;
      const canais = (cor: string) => { ctx.clearRect(0, 0, 1, 1); ctx.fillStyle = cor; ctx.fillRect(0, 0, 1, 1); return [...ctx.getImageData(0, 0, 1, 1).data]; };
      let fundo: Element | null = el;
      while (fundo && canais(getComputedStyle(fundo).backgroundColor)[3] === 0) fundo = fundo.parentElement;
      const luminancia = (rgb: number[]) => rgb.slice(0, 3).map(n => n / 255).map(n => n <= .04045 ? n / 12.92 : ((n + .055) / 1.055) ** 2.4)
        .reduce((s, n, i) => s + n * [.2126, .7152, .0722][i], 0);
      const a = luminancia(canais(css.color)), b = luminancia(canais(fundo ? getComputedStyle(fundo).backgroundColor : '#fff'));
      return { outline: css.outlineStyle, color: css.color, contrast: (Math.max(a, b) + .05) / (Math.min(a, b) + .05) };
    });
    expect(cores.contrast).toBeGreaterThanOrEqual(4.5);
    await info.attach(`estilos-${width}`, { body: JSON.stringify(cores), contentType: 'application/json' });
    await page.getByLabel('Cor', { exact: true }).fill('Azul');
    await expect(page.locator('#remover-foto')).toBeDisabled();
    await expect(dialogo).toContainText('Salve ou descarte');
    await dialogo.getByRole('button', { name: 'Descartar alterações', exact: true }).click();
    await expect(page.getByLabel('Cor', { exact: true })).toHaveValue('Prata');
    await page.locator('#arquivo-foto').setInputFiles(arquivo);
    await tabAte(page, page.getByRole('button', { name: 'Descartar seleção', exact: true }));
    await page.keyboard.press('Enter');
    await expect(page.locator('#selecionar-foto')).toBeFocused();
    await page.locator('#arquivo-foto').setInputFiles({ name: 'corrompida.png', mimeType: 'image/png', buffer: Buffer.from('nao-e-png') });
    // O browser não consegue decodificar a prévia e rejeita antes do envio.
    // Conteúdo corrompido via HTTP também tem cobertura independente no backend.
    await expect(page.locator('#erro-foto-veiculo')).toBeFocused();
    await page.screenshot({ path: info.outputPath(`angular-erro-${width}.png`) });
    await tabAte(page, page.locator('#remover-foto'));
    await page.keyboard.press('Enter');
    await expect(dialogo).toContainText('Foto removida');
    await expect(dialogo).toContainText('Sem foto');
    const footer = dialogo.locator('footer');
    expect(await footer.evaluate(el => {
      const r = el.getBoundingClientRect(); return r.top >= 0 && r.bottom <= innerHeight;
    })).toBe(true);
    await dialogo.getByRole('button', { name: 'Salvar veículo', exact: true }).focus();
    await page.keyboard.press('Tab');
    await expect(dialogo.getByRole('button', { name: 'Fechar formulário' })).toBeFocused();
    await page.keyboard.press('Shift+Tab');
    await expect(dialogo.getByRole('button', { name: 'Salvar veículo', exact: true })).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(dialogo).not.toBeVisible();
    await expect(page.getByRole('button', { name: 'Editar veículo FIC1A01', exact: true })).toBeFocused();
    await page.getByRole('button', { name: 'Editar veículo FIC1A01', exact: true }).click();
    await expect(dialogo.locator('img')).toHaveCount(0);
    await page.keyboard.press('Escape');
  });
}

test('foto: resposta perdida, reconciliação, conflito, leitura indisponível e sessão', async ({ page, request }, info) => {
  test.setTimeout(120_000);
  const { id, arquivo, headers } = await preparar(page, request);
  const rota = `**/api/v1/veiculos/${id}/foto`;
  let perder = true;
  await page.route(rota, async route => {
    if (route.request().method() === 'PUT' && perder) {
      perder = false;
      const resposta = await route.fetch();
      expect(resposta.status()).toBe(200);
      await route.abort('failed'); // commit confirmado no servidor, resposta perdida no browser
    } else await route.continue();
  });
  await page.locator('#arquivo-foto').setInputFiles(arquivo);
  await page.locator('#enviar-foto').click();
  await expect(page.getByRole('dialog')).toContainText('Resultado não confirmado');
  await expect(page.locator('#selecionar-foto')).toBeDisabled();
  await page.locator('#recarregar-foto').click();
  await expect(page.getByRole('dialog')).toContainText('referência atual');
  await expect(page.getByRole('dialog')).not.toContainText('Foto adicionada');
  await expect(page.locator('#enviar-foto')).toHaveCount(0);
  await expect(page.getByRole('dialog').locator('img')).toBeVisible();
  await page.unroute(rota);
  await page.route(rota, async route => {
    if (route.request().method() === 'GET') await route.fulfill({ status: 503, contentType: 'application/problem+json', body: '{"code":"foto_indisponivel"}' });
    else await route.continue();
  });
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Editar veículo FIC1A01', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('Não foi possível carregar a foto');
  await page.screenshot({ path: info.outputPath('angular-leitura-indisponivel.png') });
  await page.unroute(rota);
  await page.getByRole('button', { name: 'Tentar novamente', exact: true }).click();
  await expect(page.getByRole('dialog').locator('img')).toBeVisible();
  // Uma edição cadastral em outra requisição invalida o ETag mantido no editor.
  const detalhe = await request.get(`${api}/veiculos/${id}`, { headers });
  const { foto: _foto, possuiAluguelAberto: _ocupado, id: _id, ...dados } = await detalhe.json();
  expect((await request.put(`${api}/veiculos/${id}`, { headers: { ...headers, 'If-Match': detalhe.headers()['etag'] }, data: { ...dados, cor: 'Verde' } })).status()).toBe(200);
  await page.locator('#arquivo-foto').setInputFiles(arquivo);
  await page.locator('#enviar-foto').click();
  await expect(page.locator('#erro-foto-veiculo')).toBeFocused();
  await expect(page.locator('#recarregar-foto')).toBeVisible();
  await page.locator('#recarregar-foto').click();
  await expect(page.getByLabel('Cor', { exact: true })).toHaveValue('Verde');
  await request.post(`${api}/session/logout`, { headers, data: {} });
  await page.locator('#arquivo-foto').setInputFiles(arquivo);
  await page.locator('#enviar-foto').click();
  // O interceptor existente pode encaminhar para login; nenhum blob permanece.
  await expect(page.locator('app-foto-veiculo img')).toHaveCount(0);
});

test('protótipo: paridade de cenários, teclado e dimensões previstas', async ({ page }, info) => {
  await page.goto(`file://${path.resolve(__dirname, '../../../openspec/changes/fase-5-fotos-privadas/prototypes/foto-veiculo.html')}`);
  for (const width of [1440, 390, 720]) {
    await page.setViewportSize({ width, height: 900 });
    await page.screenshot({ path: info.outputPath(`prototipo-ativo-${width}.png`) });
    for (const estado of ['empty', 'new', 'read-error', 'write-error', 'invalid', 'rented', 'stale', 'race', 'uncertain', 'session', 'occupied', 'timeout', 'active', 'new-write-error', 'new-uncertain']) {
      await page.locator('#scenario').selectOption(estado);
      await expect(page.getByRole('dialog')).toBeVisible();
      if (['rented', 'stale', 'session'].includes(estado)) {
        await expect(page.locator('#photo-notice')).toBeVisible();
      }
    }
    for (const estado of ['new', 'new-write-error', 'new-uncertain']) {
      await page.locator('#scenario').selectOption(estado);
      expect(await page.locator('#capacity').evaluate(el =>
        !!(el.compareDocumentPosition(document.getElementById('choose')!) & Node.DOCUMENT_POSITION_FOLLOWING))).toBe(true);
      await page.locator('#choose').click();
      await expect(page.locator('#send')).not.toBeVisible();
      await expect(page.locator('#save')).toBeFocused();
      await page.keyboard.press('Enter');
      if (estado === 'new') await expect(page.locator('#message')).toContainText('Veículo e foto salvos.');
      if (estado === 'new-write-error') await expect(page.locator('#error-summary')).toContainText('Veículo salvo, mas');
      if (estado === 'new-uncertain') await expect(page.locator('#message')).toContainText('Veículo salvo, mas');
      await page.locator('#choose').scrollIntoViewIfNeeded();
      await page.screenshot({ path: info.outputPath(`prototipo-${estado}-${width}.png`) });
    }
    expect(await page.getByRole('dialog').evaluate(el => el.scrollWidth <= el.clientWidth)).toBe(true);
  }
});
