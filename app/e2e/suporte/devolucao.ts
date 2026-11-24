import { randomUUID } from 'node:crypto';
import { expect, type APIRequestContext, type Page } from '@playwright/test';

export const api = 'http://127.0.0.1:5285/api/v1';
export const senha = 'abcdefgh';

export async function prepararDevolucao(request: APIRequestContext) {
  const usuario = `f4${Date.now().toString(36)}${randomUUID().slice(0, 5)}`;
  expect((await request.post(`${api}/registro`, { data: { username: usuario, email: `${usuario}@example.invalid`, password: senha } })).status()).toBe(201);
  const login = await request.post(`${api}/session/login`, { data: { username: usuario, password: senha } });
  expect(login.status()).toBe(200);
  const sessao = await login.json();
  const headers = { Authorization: `Bearer ${sessao.accessToken}` };
  async function criar(rota: string, data: unknown) {
    const r = await request.post(`${api}/${rota}`, { headers, data });
    expect(r.status(), rota).toBe(201);
    return r.json();
  }
  const grupo = await criar('grupos', { nome: 'Compactos sintéticos F4' });
  const plano = await criar('planos', { grupoId: grupo.id, modalidade: 'Livre', precoDiario: '100.00', precoKm: null, franquiaDiariaKm: null, precoKmExcedente: null });
  const veiculo = await criar('veiculos', { placa: 'FIC4A01', marca: 'Marca sintética', modelo: 'Modelo F4', cor: 'Azul',
    combustivel: 'Gasolina', capacidadeLitros: '50.000', ano: 2026, grupoId: grupo.id });
  const cliente = await criar('clientes', { tipo: 'PF', nome: 'Cliente sintético F4', cpf: '52998224725', rg: 'RG-sintetico',
    cnh: 'CNH-sintetica-418', telefone: '11900001842', email: null,
    endereco: { estado: 'SP', cidade: 'São Paulo', bairro: 'Centro', rua: 'Rua sintética', numero: '10' } });
  const condutor = await criar('condutores', { clienteId: cliente.id, proprioCliente: true, nome: cliente.nome, cpf: '52998224725',
    cnh: 'CNH-sintetica-418', validadeCnh: '2035-12-31', telefone: '11900001842', email: 'condutor@example.invalid' });
  const fixa = await criar('taxas-servicos', { nome: 'Higienização sintética', preco: '30.00', modalidade: 'Fixa' });
  const diaria = await criar('taxas-servicos', { nome: 'Apoio de viagem sintético', preco: '10.00', modalidade: 'Diaria' });
  for (const [tipo, precoDiario] of [['Cliente', '5.00'], ['Terceiros', '7.00']])
    expect((await request.put(`${api}/configuracoes/seguros/${tipo}`, { headers, data: { precoDiario } })).status()).toBe(201);
  expect((await request.put(`${api}/configuracoes/combustiveis/Gasolina`, { headers, data: { precoUnitario: '6.00' } })).status()).toBe(201);
  const retorno = new Date(Date.now() + 47 * 3_600_000);
  const retornoLocal = new Intl.DateTimeFormat('sv-SE', { timeZone: 'America/Sao_Paulo',
    year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(retorno).replace(' ', 'T');
  const comando = { clienteId: cliente.id, condutorId: condutor.id, veiculoId: veiculo.id, planoId: plano.id,
    retornoPrevisto: `${retornoLocal}:00-03:00`, hodometroInicial: 18420, taxasServicoIds: [fixa.id], seguros: ['Cliente', 'Terceiros'] };
  const resposta = await request.post(`${api}/alugueis`, { headers: { ...headers, 'Idempotency-Key': randomUUID() }, data: comando });
  expect(resposta.status()).toBe(201);
  const contrato = await resposta.json();
  return { usuario, headers, sessao, comando, contrato, etag: resposta.headers()['etag'], retornoLocal, diaria, veiculo };
}

export async function abrirDevolucao(page: Page, dados: Awaited<ReturnType<typeof prepararDevolucao>>) {
  await page.goto('/entrar');
  await page.getByLabel('Usuário', { exact: true }).fill(dados.usuario);
  await page.getByLabel('Senha', { exact: true }).fill(senha);
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page).toHaveURL(/\/painel$/);
  await page.getByRole('link', { name: 'Aluguéis', exact: true }).click();
  await page.getByRole('button', { name: 'Registrar devolução', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'Registrar devolução', exact: true })).toBeFocused();
  await page.getByLabel('Data e hora real da devolução', { exact: true }).fill(dados.retornoLocal);
  await page.getByLabel('Hodômetro final (km)', { exact: true }).fill('18570');
  await page.getByLabel('Litros restantes no tanque', { exact: true }).fill('40');
  await page.getByRole('checkbox', { name: /Apoio de viagem sintético/ }).focus();
  await page.keyboard.press('Space');
  await page.getByRole('button', { name: 'Calcular prévia', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByLabel('Demonstrativo da devolução')).toContainText('334,00');
}
