using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Tipo do membro que corre na borda da laje (nas linhas de apoio, onde as vigas terminam).
    /// Para vigas convencionais a borda e a guia "U" (travamento leve). Para vigas trelicadas a
    /// borda passa a ser uma trelica de travamento, que recebe as pontas das trelicas nos dois
    /// apoios -- nao se aplica a guia "U" como se a estrutura fosse de vigas convencionais.
    /// </summary>
    public enum TipoMembroBorda
    {
        /// <summary>Guia "U" de borda (vigas convencionais: perfil simples, caixao, back-to-back).</summary>
        GuiaBorda,

        /// <summary>Trelica transversal de travamento (vigas trelicadas).</summary>
        TrelicaTravamento
    }

    /// <summary>
    /// Regra pura do travamento das trelicas da laje. NAO dimensiona nada: apenas decide o tipo
    /// do membro de borda e identifica as linhas de apoio (as duas bordas do envolvente) onde o
    /// travamento encontra as trelicas. A regra de quantidade/posicao de travamentos entre
    /// trelicas e das fitas tensionadas e geometrica de partida, NAO normativa (ver FitasTensionadasLaje).
    /// </summary>
    public static class TravamentoLaje
    {
        /// <summary>
        /// Membro que corre na borda da laje, conforme o tipo de viga escolhido.
        /// Trelica -> trelica de travamento; demais tipos -> guia "U".
        /// </summary>
        public static TipoMembroBorda MembroDeBorda(TipoVigaLaje tipoViga)
        {
            return tipoViga == TipoVigaLaje.Trelica
                ? TipoMembroBorda.TrelicaTravamento
                : TipoMembroBorda.GuiaBorda;
        }

        /// <summary>
        /// Posicoes u (mm) das linhas de apoio da laje no envolvente orientado: as duas bordas
        /// (inicio e fim do vao), onde as vigas/trelicas terminam e recebem o travamento. Lista
        /// vazia se a orientacao for nula.
        /// </summary>
        public static List<double> Apoios(OrientacaoVigas orientacao)
        {
            var apoios = new List<double>();
            if (orientacao == null) return apoios;

            apoios.Add(orientacao.UMinMm);
            apoios.Add(orientacao.UMaxMm);
            return apoios;
        }
    }
}
