using System;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Segmento reto de contorno (aresta) em milimetros, com os dois extremos. Base da montagem
    /// de um contorno fechado a partir de bordas existentes (linhas de modelo detalhe) ou do
    /// desenho por pontos: o ContornoLaje.MontarDeSegmentos ordena segmentos e fecha o poligono.
    /// </summary>
    public readonly struct SegmentoContorno : IEquatable<SegmentoContorno>
    {
        public SegmentoContorno(Ponto2DMm inicio, Ponto2DMm fim)
        {
            Inicio = inicio;
            Fim = fim;
        }

        public Ponto2DMm Inicio { get; }

        public Ponto2DMm Fim { get; }

        public double ComprimentoMm => Inicio.DistanciaAte(Fim);

        /// <summary>true quando os extremos coincidem (segmento desprezivel).</summary>
        public bool EhDegenerado => Inicio.EstaProximoDe(Fim, ContornoLaje.ToleranciaMm);

        /// <summary>Segmento visto do outro extremo para a frente (usado ao encadear arestas).</summary>
        public SegmentoContorno Invertido => new SegmentoContorno(Fim, Inicio);

        public bool Equals(SegmentoContorno outro)
        {
            return (Inicio.Equals(outro.Inicio) && Fim.Equals(outro.Fim))
                || (Inicio.Equals(outro.Fim) && Fim.Equals(outro.Inicio));
        }

        public override bool Equals(object obj)
        {
            return obj is SegmentoContorno outro && Equals(outro);
        }

        public override int GetHashCode()
        {
            // Ordem-invariante: o hash nao muda se o segmento for invertido.
            int h1 = Inicio.GetHashCode();
            int h2 = Fim.GetHashCode();
            return h1 ^ h2;
        }

        public override string ToString()
        {
            return Inicio + " -> " + Fim;
        }
    }
}
