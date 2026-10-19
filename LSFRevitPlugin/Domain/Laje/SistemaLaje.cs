using System;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Sistema construtivo da laje. Hoje so existe um: grade de vigas LSF de perfil simples
    /// apoiadas por guias de borda. O enum existe para a interface NAO oferecer opcoes falsas
    /// enquanto prepara o terreno para sistemas futuros (caixao, back-to-back, trelica), que
    /// entram como valores novos sem quebrar quem ja usa o sistema atual.
    /// </summary>
    public enum SistemaLaje
    {
        /// <summary>Grade de vigas LSF de perfil simples (alma vertical) + guias de borda.</summary>
        VigasLsfComGuiasBorda
    }

    /// <summary>
    /// Descricao de um sistema de laje, com o que efetivamente esta implementado na geracao.
    /// Nao vende capacidade estrutural: diz apenas o que o plugin gera hoje.
    /// </summary>
    public sealed class DescricaoSistemaLaje
    {
        public DescricaoSistemaLaje(SistemaLaje sistema, string nome, string descricao, bool implementado)
        {
            Sistema = sistema;
            Nome = nome ?? throw new ArgumentNullException(nameof(nome));
            Descricao = descricao ?? throw new ArgumentNullException(nameof(descricao));
            Implementado = implementado;
        }

        public SistemaLaje Sistema { get; }

        public string Nome { get; }

        public string Descricao { get; }

        /// <summary>
        /// true quando a geracao consegue criar geometria deste sistema. false marca opcoes
        /// modeladas no dominio mas ainda sem geometria 3D, para a interface nao mostrar
        /// seletor sem efeito.
        /// </summary>
        public bool Implementado { get; }

        public string Resumo() => Nome;
    }

    /// <summary>
    /// Catalogo dos sistemas de laje. Só o sistema implementado e apresentado na interface.
    /// </summary>
    public static class CatalogoSistemasLaje
    {
        public static readonly DescricaoSistemaLaje VigasLsfComGuiasBorda = new DescricaoSistemaLaje(
            SistemaLaje.VigasLsfComGuiasBorda,
            "Vigas LSF com guias de borda",
            "Grade de vigas LSF de perfil simples (alma vertical) distribuidas por espacamento, "
            + "com guias de borda em U nas arestas onde as vigas terminam. Unico sistema com geracao 3D hoje.",
            implementado: true);

        public static readonly DescricaoSistemaLaje[] Todos =
        {
            VigasLsfComGuiasBorda
        };

        /// <summary>Descrição de um sistema; nulo se ele nao existir no catalogo.</summary>
        public static DescricaoSistemaLaje Obter(SistemaLaje sistema)
        {
            foreach (DescricaoSistemaLaje d in Todos)
            {
                if (d.Sistema == sistema) return d;
            }

            return null;
        }
    }
}
