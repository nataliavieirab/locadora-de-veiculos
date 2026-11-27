using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Fotos;

public enum TipoTrabalhoObjeto { CompensarNuncaAtivo, RemocaoRetida }
public enum EstadoTrabalhoObjeto { Pendente, EmExecucao, Concluido, FalhaFinal, Retido }

public sealed class TrabalhoObjeto : EntidadeCadastro
{
    public Guid FotoObjetoId { get; private set; }
    public TipoTrabalhoObjeto Tipo { get; private set; }
    public EstadoTrabalhoObjeto Estado { get; private set; }
    public Guid? TokenPosse { get; private set; }
    public DateTimeOffset? LeaseAteUtc { get; private set; }
    public int Tentativas { get; private set; }
    public DateTimeOffset ProximoProcessamentoUtc { get; private set; }
    public string? CodigoFalha { get; private set; }
    public DateTimeOffset? UltimaFalhaEmUtc { get; private set; }
    public DateTimeOffset? EnvioConfirmadoEmUtc { get; private set; }

    private TrabalhoObjeto() { }
    private TrabalhoObjeto(Guid empresaId, DateTimeOffset agora) : base(empresaId, agora) { }

    public static TrabalhoObjeto Criar(FotoObjeto foto, DateTimeOffset agora)
    {
        if (agora < foto.AlteradoEmUtc)
            throw new ArgumentException("O trabalho não pode preceder a foto.", nameof(agora));
        var retido = foto.Estado is EstadoFoto.Ativa or EstadoFoto.Inativa;
        return new TrabalhoObjeto(foto.EmpresaId, agora)
        {
            FotoObjetoId = foto.Id,
            Tipo = retido ? TipoTrabalhoObjeto.RemocaoRetida : TipoTrabalhoObjeto.CompensarNuncaAtivo,
            Estado = retido ? EstadoTrabalhoObjeto.Retido : EstadoTrabalhoObjeto.Pendente,
            ProximoProcessamentoUtc = agora.ToUniversalTime()
        };
    }

    public Guid Adquirir(DateTimeOffset agora, FotoObjeto? foto = null)
    {
        if ((Tipo == TipoTrabalhoObjeto.RemocaoRetida
                && (foto is null || foto.Id != FotoObjetoId || foto.EmpresaId != EmpresaId
                    || !foto.ElegivelParaRemocao(agora)))
            || Estado is EstadoTrabalhoObjeto.Concluido or EstadoTrabalhoObjeto.FalhaFinal
            || ProximoProcessamentoUtc > agora || LeaseAteUtc > agora)
            throw new InvalidOperationException("O trabalho não está disponível.");
        RegistrarAlteracao(agora);
        Estado = EstadoTrabalhoObjeto.EmExecucao;
        TokenPosse = Guid.NewGuid();
        LeaseAteUtc = agora.AddMinutes(2).ToUniversalTime();
        return TokenPosse.Value;
    }

    public void Concluir(Guid token, FotoObjeto foto, DateTimeOffset agora)
    {
        ExigirPosse(token, agora);
        if (foto.Id != FotoObjetoId || foto.EmpresaId != EmpresaId
            || (Tipo == TipoTrabalhoObjeto.CompensarNuncaAtivo
                ? foto.Estado != EstadoFoto.Abandonada || !EnvioConfirmado
                : !foto.ElegivelParaRemocao(agora)))
            throw new InvalidOperationException("O objeto não permite concluir remoção.");
        RegistrarAlteracao(agora);
        Estado = EstadoTrabalhoObjeto.Concluido;
        TokenPosse = null;
        LeaseAteUtc = null;
    }

    public void Reter(Guid token, FotoObjeto foto, DateTimeOffset agora)
    {
        ExigirPosse(token, agora);
        if (foto.Id != FotoObjetoId || foto.EmpresaId != EmpresaId
            || foto.Estado is not (EstadoFoto.Ativa or EstadoFoto.Inativa))
            throw new InvalidOperationException("A retenção exige objeto anteriormente ativo.");
        RegistrarAlteracao(agora);
        Tipo = TipoTrabalhoObjeto.RemocaoRetida;
        Estado = EstadoTrabalhoObjeto.Retido;
        TokenPosse = null;
        LeaseAteUtc = null;
    }

    public void AgendarRetencao(FotoObjeto foto, DateTimeOffset agora)
    {
        if (foto.Id != FotoObjetoId || foto.EmpresaId != EmpresaId || foto.Estado != EstadoFoto.Inativa
            || foto.ExcluirAposUtc is null || Tipo != TipoTrabalhoObjeto.RemocaoRetida
            || Estado != EstadoTrabalhoObjeto.Retido)
            throw new InvalidOperationException("A foto não permite agendar retenção.");
        RegistrarAlteracao(agora);
        ProximoProcessamentoUtc = foto.ExcluirAposUtc.Value;
        Estado = EstadoTrabalhoObjeto.Pendente;
    }

    public void RegistrarFalha(Guid token, DateTimeOffset agora) =>
        RegistrarFalha(token, agora, EnvioConfirmado ? "remocao_indisponivel" : "armazenamento_indisponivel");

    public void RegistrarProtecao(Guid token, DateTimeOffset agora) =>
        RegistrarFalha(token, agora, "referencia_protegida");

    private void RegistrarFalha(Guid token, DateTimeOffset agora, string codigo)
    {
        ExigirPosse(token, agora);
        RegistrarAlteracao(agora);
        Tentativas = Tentativas == int.MaxValue ? int.MaxValue : Tentativas + 1;
        Estado = EstadoTrabalhoObjeto.Pendente;
        CodigoFalha = codigo;
        UltimaFalhaEmUtc = agora.ToUniversalTime();
        var segundos = Tentativas >= 13 ? 86400 : Math.Min(86400, 30L << (Tentativas - 1));
        ProximoProcessamentoUtc = agora.AddSeconds(segundos).ToUniversalTime();
        TokenPosse = null;
        LeaseAteUtc = null;
    }

    // O ack durável só é registrado depois da conclusão observada do PUT.
    // Sem ele, ausência do objeto NÃO prova que um PUT tardio não ocorrerá.
    public bool EnvioConfirmado => EnvioConfirmadoEmUtc is not null;

    public void ConfirmarEnvio(Guid token, DateTimeOffset agora)
    {
        ExigirPosse(token, agora);
        RegistrarAlteracao(agora);
        EnvioConfirmadoEmUtc ??= agora.ToUniversalTime();
    }

    public void ExigirPosse(Guid token, DateTimeOffset agora)
    {
        if (Estado != EstadoTrabalhoObjeto.EmExecucao || token == Guid.Empty
            || TokenPosse != token || LeaseAteUtc <= agora)
            throw new InvalidOperationException("A posse do trabalho não é válida.");
    }
}
