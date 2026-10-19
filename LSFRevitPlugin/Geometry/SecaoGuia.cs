using System;
using System.Collections.Generic;
using LSFRevitPlugin.Core;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Contorno transversal da secao de guia/trilho (track) em "U" -- alma + duas abas, SEM
    /// labios -- em funcao pura, sem depender de um Document do Revit. E a secao usada pelas
    /// guias da parede, pela verga/contra-verga e pelo blocking horizontal (guia cortada e
    /// dobrada), e pode ser exercitada pelos testes sem abrir o Revit.
    ///
    /// As ENTRADAS sao em milimetros; o contorno devolvido esta em unidades internas do Revit.
    /// </summary>
    public static class SecaoGuia
    {
        /// <summary>
        /// Contorno do "U", em sequencia fechada, no plano transversal:
        /// V = eixo da alma (no trecho horizontal, perpendicular a parede);
        /// Z = eixo vertical (para onde as abas crescem a partir da alma).
        /// 8 vertices. <paramref name="abertoParaCima"/> = true: alma embaixo (Z ~ 0) e abas
        /// para cima; false: espelhado em Z, com o mesmo sentido de percurso.
        /// </summary>
        public static List<(double V, double Z)> ContornoU(
            double larguraAlmaMm, double alturaAbaMm, double espessuraChapaMm, bool abertoParaCima)
        {
            double t = SecaoPerfil.EspessuraModeladaInterna(espessuraChapaMm);
            // Largura externa da guia = alma do montante + 2 chapas: o montante encaixa por dentro.
            double meiaLargura = RevitVersionAdapter.MilimetrosParaInterno(larguraAlmaMm) / 2.0 + t;
            double alturaAba = RevitVersionAdapter.MilimetrosParaInterno(alturaAbaMm);

            if (abertoParaCima)
            {
                return new List<(double V, double Z)>
                {
                    (-meiaLargura, 0),
                    (meiaLargura, 0),
                    (meiaLargura, alturaAba),
                    (meiaLargura - t, alturaAba),
                    (meiaLargura - t, t),
                    (-meiaLargura + t, t),
                    (-meiaLargura + t, alturaAba),
                    (-meiaLargura, alturaAba),
                };
            }

            // Espelha em Z (z' = alturaAba - z) e inverte a ordem, para manter o mesmo sentido.
            return new List<(double V, double Z)>
            {
                (-meiaLargura, 0),
                (-meiaLargura + t, 0),
                (-meiaLargura + t, alturaAba - t),
                (meiaLargura - t, alturaAba - t),
                (meiaLargura - t, 0),
                (meiaLargura, 0),
                (meiaLargura, alturaAba),
                (-meiaLargura, alturaAba),
            };
        }
    }
}
