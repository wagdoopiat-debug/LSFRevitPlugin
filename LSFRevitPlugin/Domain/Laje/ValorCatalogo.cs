using System;
using System.Globalization;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Um valor do catalogo, imutavel, com nome, unidade, origem e fonte. Nome, unidade e fonte
    /// nunca ficam em branco: o catalogo nao admite valor sem rastro de onde veio.
    /// </summary>
    public sealed class ValorCatalogo
    {
        public ValorCatalogo(string nome, double valor, string unidade, OrigemValor origem, string fonte)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new ArgumentException("Nome do valor e obrigatorio.", nameof(nome));
            if (string.IsNullOrWhiteSpace(unidade))
                throw new ArgumentException("Unidade do valor e obrigatoria.", nameof(unidade));
            if (string.IsNullOrWhiteSpace(fonte))
                throw new ArgumentException("Fonte do valor e obrigatoria.", nameof(fonte));
            if (double.IsNaN(valor) || double.IsInfinity(valor))
                throw new ArgumentException("Valor deve ser um numero finito.", nameof(valor));

            Nome = nome;
            Valor = valor;
            Unidade = unidade;
            Origem = origem;
            Fonte = fonte;
        }

        public string Nome { get; }

        public double Valor { get; }

        public string Unidade { get; }

        public OrigemValor Origem { get; }

        public string Fonte { get; }

        /// <summary>
        /// Copia com outro valor, origem e fonte (por exemplo, ao trocar um valor de referencia
        /// pelo da ficha oficial). Nome e unidade permanecem.
        /// </summary>
        public ValorCatalogo ComValor(double novoValor, OrigemValor novaOrigem, string novaFonte)
        {
            return new ValorCatalogo(Nome, novoValor, Unidade, novaOrigem, novaFonte);
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} = {1:0.###} {2} [{3}; {4}]",
                Nome, Valor, Unidade, Origem, Fonte);
        }
    }
}
