namespace LSFRevitPlugin.Domain.Models
{
    /// <summary>
    /// Um painel de uma parede: um trecho do eixo, em unidades internas do Revit, medido a
    /// partir da ponta inicial da parede (a ponta onde a linha de localizacao comeca).
    /// Um painel nao e necessariamente uma parede inteira.
    /// </summary>
    public class PainelInfo
    {
        /// <summary>Numero do painel dentro da parede, a partir de 1 (do inicio para o fim).</summary>
        public int Numero { get; set; }

        /// <summary>Distancia do inicio da parede ate o comeco do painel.</summary>
        public double InicioInterno { get; set; }

        /// <summary>Distancia do inicio da parede ate o fim do painel.</summary>
        public double FimInterno { get; set; }

        public double LarguraInterna
        {
            get { return FimInterno - InicioInterno; }
        }
    }
}