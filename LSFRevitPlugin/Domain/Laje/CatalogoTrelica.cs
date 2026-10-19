using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Catalogo extensivel dos tipos de trelica. Diz quais tipos ja geram geometria 3D
    /// (Implementado) e os parametros geometricos de partida. Acrescentar um tipo novo e so
    /// somar uma entrada aqui e o caso correspondente em ComposicaoTrelica.Membros.
    /// </summary>
    public static class CatalogoTrelica
    {
        public sealed class TrelicaInfo
        {
            public TrelicaTipo Tipo { get; set; }
            public bool Implementado { get; set; }
            public string Nome { get; set; }
            public string Descricao { get; set; }
        }

        public static readonly List<TrelicaInfo> Todos = new List<TrelicaInfo>
        {
            new TrelicaInfo
            {
                Tipo = TrelicaTipo.Warren, Implementado = true, Nome = "Warren",
                Descricao = "Zigue-zague de diagonais alternadas, sem montantes verticais."
            },
            new TrelicaInfo
            {
                Tipo = TrelicaTipo.Pratt, Implementado = true, Nome = "Pratt",
                Descricao = "Montantes verticais e diagonais inclinadas para o centro."
            },
            new TrelicaInfo
            {
                Tipo = TrelicaTipo.Howe, Implementado = true, Nome = "Howe",
                Descricao = "Montantes verticais e diagonais inclinadas para fora."
            },
            new TrelicaInfo
            {
                Tipo = TrelicaTipo.DiagonaisAlternadas, Implementado = true, Nome = "Diagonais alternadas",
                Descricao = "Montantes verticais e diagonais alternadas a cada painel."
            },
            new TrelicaInfo
            {
                Tipo = TrelicaTipo.MontantesVerticais, Implementado = true, Nome = "Montantes verticais",
                Descricao = "Somente banzos e montantes verticais internos (sem diagonais)."
            }
        };

        public static TrelicaInfo Obter(TrelicaTipo tipo)
        {
            foreach (TrelicaInfo info in Todos)
            {
                if (info.Tipo == tipo) return info;
            }

            return null;
        }

        public static bool Implementado(TrelicaTipo tipo)
        {
            TrelicaInfo info = Obter(tipo);
            return info != null && info.Implementado;
        }
    }
}
