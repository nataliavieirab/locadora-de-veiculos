import { expect, test, type Locator, type Page } from '@playwright/test';

const mensagemCredencial = 'Não foi possível entrar. Verifique os dados e tente de novo.';

test.describe.configure({ mode: 'serial' });
test.setTimeout(90_000);

test('registro, login, logout, recarga e duas empresas nas duas origens', async ({ browser }) => {
  const sufixo = Date.now().toString(36);
  const senha = 'abcdefgh';
  const usuarioA = `empresa${sufixo}a`;
  const usuarioB = `empresa${sufixo}b`;

  const contextoA = await browser.newContext();
  const contextoB = await browser.newContext();
  const paginaA = await contextoA.newPage();
  const paginaB = await contextoB.newPage();

  await registrar(paginaA, usuarioA, `${usuarioA}@exemplo.com`, senha);
  await esperarLoginGenerico(paginaA, `ninguem${sufixo}`, 'errada12');

  const loginA = paginaA.waitForResponse((resposta) => resposta.url().includes('/api/v1/session/login'));
  await entrar(paginaA, usuarioA, senha);
  const respostaA = await loginA;
  expect(new URL(paginaA.url()).origin).toBe('http://localhost:4200');
  expect(new URL(respostaA.url()).origin).toBe('http://127.0.0.1:5285');
  expect(respostaA.headers()['access-control-allow-origin']).toBe('http://localhost:4200');
  expect(respostaA.headers()['access-control-allow-credentials']).toBeUndefined();
  await expect(paginaA.locator('#empresa')).toBeVisible();
  const empresaA = (await paginaA.locator('#empresa').innerText()).trim();
  await armazenamentoSemToken(paginaA);

  await registrar(paginaB, usuarioB, `${usuarioB}@exemplo.com`, senha);
  await entrar(paginaB, usuarioB, senha);
  const empresaB = (await paginaB.locator('#empresa').innerText()).trim();
  expect(empresaA).not.toBe(empresaB);
  expect(empresaA).not.toBe('');

  const abaNova = await contextoA.newPage();
  await abaNova.goto('/painel');
  await expect(abaNova).toHaveURL(/\/entrar/);
  await expect(paginaA.locator('#empresa')).toHaveText(empresaA);

  await paginaA.getByRole('button', { name: 'Sair' }).click();
  await expect(paginaA).toHaveURL(/\/entrar/);
  await paginaA.reload();
  await expect(paginaA).toHaveURL(/\/entrar/);
  await expect(paginaB.locator('#empresa')).toHaveText(empresaB);

  await paginaB.reload();
  await expect(paginaB).toHaveURL(/\/entrar/);

  await contextoA.close();
  await contextoB.close();
});

async function registrar(pagina: Page, usuario: string, email: string, senha: string): Promise<void> {
  await pagina.goto('/registrar');
  await preencher(pagina.locator('#usuario'), usuario);
  await preencher(pagina.locator('#email'), email);
  await preencher(pagina.locator('#senha'), senha);
  await pagina.getByRole('button', { name: 'Cadastrar' }).click();
  await expect(pagina).toHaveURL(/\/entrar/);
  await expect(pagina.getByText('Conta criada. Entre com seu usuário e senha.')).toBeVisible();
}

async function entrar(pagina: Page, usuario: string, senha: string): Promise<void> {
  await pagina.goto('/entrar');
  await preencher(pagina.locator('#usuario'), usuario);
  await preencher(pagina.locator('#senha'), senha);
  await pagina.getByRole('button', { name: 'Entrar' }).click();
  await expect(pagina).toHaveURL(/\/painel/);
}

async function esperarLoginGenerico(pagina: Page, usuario: string, senha: string): Promise<void> {
  await pagina.goto('/entrar');
  await preencher(pagina.locator('#usuario'), usuario);
  await preencher(pagina.locator('#senha'), senha);
  await pagina.getByRole('button', { name: 'Entrar' }).click();
  const alerta = pagina.locator('#resumo-erro');
  await expect(alerta).toHaveText(mensagemCredencial);
  await expect(alerta).toBeFocused();
  await expect(alerta).not.toContainText(/não existe|nao existe|incorreta/i);
}

async function preencher(campo: Locator, valor: string): Promise<void> {
  await expect(async () => {
    await campo.click();
    await campo.fill('');
    await campo.pressSequentially(valor, { delay: 15 });
    await campo.evaluate(() => new Promise((resolve) => setTimeout(resolve, 50)));
    await expect(campo).toHaveValue(valor);
  }).toPass();
}

async function armazenamentoSemToken(pagina: Page): Promise<void> {
  const texto = await pagina.evaluate(async () => {
    const partes = [document.cookie];
    for (let indice = 0; indice < localStorage.length; indice += 1) {
      const chave = localStorage.key(indice) ?? '';
      partes.push(chave, localStorage.getItem(chave) ?? '');
    }
    for (let indice = 0; indice < sessionStorage.length; indice += 1) {
      const chave = sessionStorage.key(indice) ?? '';
      partes.push(chave, sessionStorage.getItem(chave) ?? '');
    }
    if (indexedDB.databases) {
      const bancos = await indexedDB.databases();
      for (const banco of bancos) {
        partes.push(banco.name ?? '');
      }
    }
    return partes.join('\n');
  });
  expect(texto).not.toContain('eyJ');
  expect(texto.trim()).toBe('');
}
