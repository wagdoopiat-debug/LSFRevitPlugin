namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Secao transversal dos banzos da trelica. "C" mantem os labios de enrijecimento do perfil
    /// escolhido; "U" remove os labios, deixando o canal simples. Vale para as duas montagens
    /// (Encaixada e FaceComFace).
    /// </summary>
    public enum SecaoBanzoTrelica
    {
        C,
        U
    }

    /// <summary>Regra pura do labio dos banzos, compartilhada pela geometria e pelos testes.</summary>
    public static class SecaoBanzoTrelicaRegra
    {
        public static double LabioMm(SecaoBanzoTrelica secao, double labioPerfilMm)
        {
            if (secao == SecaoBanzoTrelica.U) return 0.0;
            if (double.IsNaN(labioPerfilMm) || double.IsInfinity(labioPerfilMm) || labioPerfilMm < 0.0)
                return 0.0;

            return labioPerfilMm;
        }
    }
}
