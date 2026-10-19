using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    public static class CatalogoTiposVigaLaje
    {
        public sealed class TipoVigaInfo
        {
            public TipoVigaLaje Tipo { get; set; }
            public bool Implementado { get; set; }
            public string Descricao { get; set; }
        }

        public static readonly List<TipoVigaInfo> Todos = new List<TipoVigaInfo>
        {
            new TipoVigaInfo { Tipo = TipoVigaLaje.PerfilSimples, Implementado = true, Descricao = "Perfil simples (C)" },
            new TipoVigaInfo { Tipo = TipoVigaLaje.BackToBack, Implementado = true, Descricao = "Composto - Back to Back (2 C costas com costas)" },
            new TipoVigaInfo { Tipo = TipoVigaLaje.Caixao, Implementado = true, Descricao = "Composto - Caixao (2 C com aberturas frente a frente)" },
            new TipoVigaInfo { Tipo = TipoVigaLaje.Trelica, Implementado = true, Descricao = "Trelica (banzos + diagonais/montantes; ver tipo de trelica)" }
        };
    }
}
