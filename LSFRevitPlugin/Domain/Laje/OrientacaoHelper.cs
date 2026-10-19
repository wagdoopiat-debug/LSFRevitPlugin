using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    public static class OrientacaoHelper
    {
        public static OrientacaoVigasModo MapearParaModo(OrientacaoUI ui, bool perpendicular)
        {
            switch (ui)
            {
                case OrientacaoUI.Manual:
                    return OrientacaoVigasModo.Manual;
                case OrientacaoUI.PorBordaReferencia:
                    return perpendicular ? OrientacaoVigasModo.PerpendicularAresta : OrientacaoVigasModo.ParalelaAresta;
                default:
                    return OrientacaoVigasModo.Automatica;
            }
        }

        public static OrientacaoUI MapearParaUI(OrientacaoVigasModo modo)
        {
            switch (modo)
            {
                case OrientacaoVigasModo.ParalelaAresta:
                case OrientacaoVigasModo.PerpendicularAresta:
                    return OrientacaoUI.PorBordaReferencia;
                case OrientacaoVigasModo.Manual:
                    return OrientacaoUI.Manual;
                default:
                    return OrientacaoUI.Automatica;
            }
        }

        public static bool EhPorBorda(OrientacaoVigasModo modo)
        {
            return modo == OrientacaoVigasModo.ParalelaAresta
                || modo == OrientacaoVigasModo.PerpendicularAresta;
        }
    }
}
