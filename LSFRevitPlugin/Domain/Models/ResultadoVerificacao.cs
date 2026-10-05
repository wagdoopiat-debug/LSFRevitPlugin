namespace LSFRevitPlugin.Domain.Models
{
    /// <summary>
    /// Resultado detalhado de uma verificacao estrutural -- guarda nao so se passou ou nao,
    /// mas qual foi o modo critico e a taxa de utilizacao, necessarios para o memorial de calculo.
    /// </summary>
    public class ResultadoVerificacao
    {
        public bool Aprovado { get; set; }

        /// <summary>Qual verificacao "reprovou" o perfil (ou "Nenhum" se aprovado em tudo).</summary>
        public string ModoCritico { get; set; } = "Nenhum";

        /// <summary>Razao entre solicitante e resistente (1.0 = no limite, maior que 1.0 = reprovado).</summary>
        public double TaxaUtilizacao { get; set; }

        /// <summary>Mensagem legivel para exibir ao usuario ou registrar em log.</summary>
        public string Observacao { get; set; } = string.Empty;
    }
}
