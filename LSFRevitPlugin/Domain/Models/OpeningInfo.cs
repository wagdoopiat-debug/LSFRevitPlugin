using Autodesk.Revit.DB;

namespace LSFRevitPlugin.Domain.Models
{
    /// <summary>
    /// Representa um vao (porta ou janela) detectado numa parede, com a posicao central
    /// projetada sobre o eixo da parede e as dimensoes do vao.
    /// </summary>
    public class OpeningInfo
    {
        public ElementId ElementoId { get; set; }
        public string Categoria { get; set; }

        /// <summary>Centro do vao, projetado sobre o eixo da parede.</summary>
        public XYZ PontoCentral { get; set; }

        /// <summary>Largura do vao (na direcao do eixo da parede), em unidades internas.</summary>
        public double LarguraInterna { get; set; }

        /// <summary>Altura do vao, em unidades internas.</summary>
        public double AlturaInterna { get; set; }

        /// <summary>Distancia do piso ate a base do vao (soleira), em unidades internas. Zero para portas.</summary>
        public double AlturaSoleiraInterna { get; set; }
    }
}
