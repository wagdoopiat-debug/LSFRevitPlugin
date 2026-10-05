using Autodesk.Revit.DB;

namespace LSFRevitPlugin.Domain.Models
{
    /// <summary>
    /// Um elemento generico de reforco de vao (King Stud, Jack Stud, cripple, verga ou
    /// contra-verga). King/Jack/cripple sao montantes verticais e usam Ponto/Direcao/Altura/
    /// Largura/Profundidade normalmente. Verga e contra-verga sao guias horizontais (mesma
    /// orientacao das guias principais da parede -- alma perpendicular a parede, aba na
    /// vertical) e trazem o solido ja pronto em SolidoPreConstruido, construido via
    /// GuiaBuilder; quando presente, os demais campos sao ignorados.
    /// </summary>
    public class ElementoReforcoVao
    {
        public XYZ Ponto { get; set; }
        public XYZ Direcao { get; set; }

        /// <summary>Altura de extrusao (no eixo Z), em unidades internas.</summary>
        public double AlturaInterna { get; set; }

        public double LarguraMm { get; set; }
        public double ProfundidadeMm { get; set; }
        public string Nome { get; set; }

        /// <summary>
        /// Solido ja construido (usado para verga/contra-verga). Quando preenchido, o
        /// StructureGenerationService usa este solido diretamente em vez de chamar
        /// StudSolidBuilder com Ponto/Direcao/Altura/Largura/Profundidade.
        /// </summary>
        public Solid SolidoPreConstruido { get; set; }
    }
}