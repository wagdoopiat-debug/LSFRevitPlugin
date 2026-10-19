using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Um valor do catalogo que ainda nao esta Conferido, com o grupo a que pertence
    /// (por exemplo "TelhaTp40").
    /// </summary>
    public sealed class PendenciaCatalogo
    {
        public PendenciaCatalogo(string grupo, ValorCatalogo valor)
        {
            if (string.IsNullOrWhiteSpace(grupo)) throw new ArgumentException("Grupo e obrigatorio.", nameof(grupo));
            if (valor == null) throw new ArgumentNullException(nameof(valor));

            Grupo = grupo;
            Valor = valor;
        }

        public string Grupo { get; }

        public ValorCatalogo Valor { get; }

        /// <summary>Filtra os valores cuja origem nao e Conferido.</summary>
        public static List<PendenciaCatalogo> Filtrar(string grupo, IEnumerable<ValorCatalogo> valores)
        {
            if (valores == null) throw new ArgumentNullException(nameof(valores));

            var lista = new List<PendenciaCatalogo>();
            foreach (ValorCatalogo v in valores)
            {
                if (v.Origem != OrigemValor.Conferido) lista.Add(new PendenciaCatalogo(grupo, v));
            }

            return lista;
        }

        public override string ToString()
        {
            return Grupo + "." + Valor;
        }
    }
}
