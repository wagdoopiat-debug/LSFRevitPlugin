namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// De onde veio o contorno da laje. Guia a interface e a persistencia: um contorno vindo de
    /// um piso existente pode ser associado ao piso (regenerecao); bordas selecionadas ou pontos
    /// desenhados sao geometria solta, sem elemento de origem para reassociar.
    /// </summary>
    public enum TipoOrigemContorno
    {
        PisoExistente,
        BordasSelecionadas,
        PontosDesenhados
    }

    /// <summary>Nome legivel da origem, para o resumo da interface.</summary>
    public static class TipoOrigemContornoTexto
    {
        public static string Nome(TipoOrigemContorno tipo)
        {
            switch (tipo)
            {
                case TipoOrigemContorno.PisoExistente: return "piso existente";
                case TipoOrigemContorno.BordasSelecionadas: return "bordas selecionadas";
                case TipoOrigemContorno.PontosDesenhados: return "pontos desenhados";
                default: return tipo.ToString();
            }
        }
    }
}
