using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Guarda a identidade de uma placa gerada -- parede, painel, face, tipo e orientacao --
    /// no proprio elemento, via Extensible Storage. E a fonte persistente nova dessa relacao.
    ///
    /// O texto no campo Comentarios continua sendo gravado com os mesmos valores (formato legado),
    /// porque e o que o usuario le no modelo e o que sustenta os projetos gerados antes deste schema
    /// existir. Os dois canais sao gravados juntos, na mesma chamada, quando aplicavel.
    ///
    /// A identidade do painel e o par (ParedeId, NumeroPainel); nao ha id proprio de painel.
    /// Schema separado do LSFPertencenciaPaineis para evitar conflitos.
    /// </summary>
    public static class PertencenciaPlacaService
    {
        private static readonly Guid SchemaGuid = new Guid("c148a621-c5af-420a-8610-6973e97354d1");
        private const string NomeSchema = "LSFPertencenciaPlacas";
        private const string CampoParedeId = "ParedeId";
        private const string CampoNumeroPainel = "NumeroPainel";
        private const string CampoFace = "Face";
        private const string CampoTipoPlaca = "TipoPlaca";
        private const string CampoOrientacao = "Orientacao";

        private static Schema ObterOuCriarSchema()
        {
            Schema existente = Schema.Lookup(SchemaGuid);
            if (existente != null) return existente;

            var builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName(NomeSchema);
            builder.AddSimpleField(CampoParedeId, typeof(int));
            builder.AddSimpleField(CampoNumeroPainel, typeof(int));
            builder.AddSimpleField(CampoFace, typeof(int));
            builder.AddSimpleField(CampoTipoPlaca, typeof(string));
            builder.AddSimpleField(CampoOrientacao, typeof(string));
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);

            return builder.Finish();
        }

        public static void Gravar(
            Element elemento,
            int paredeId,
            int numeroPainel,
            int face,
            string tipoPlaca,
            string orientacao)
        {
            if (elemento == null) return;

            Schema schema = ObterOuCriarSchema();
            var entidade = new Entity(schema);
            entidade.Set(CampoParedeId, paredeId);
            entidade.Set(CampoNumeroPainel, numeroPainel);
            entidade.Set(CampoFace, face);
            entidade.Set(CampoTipoPlaca, tipoPlaca ?? string.Empty);
            entidade.Set(CampoOrientacao, orientacao ?? string.Empty);
            elemento.SetEntity(entidade);
        }

        public static bool TentarLer(Element elemento, out IdentidadePlaca identidade)
        {
            identidade = null;
            if (elemento == null) return false;

            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema == null) return false;

            Entity entidade = elemento.GetEntity(schema);
            if (entidade == null || !entidade.IsValid()) return false;

            try
            {
                identidade = new IdentidadePlaca
                {
                    ParedeId = entidade.Get<int>(CampoParedeId),
                    NumeroPainel = entidade.Get<int>(CampoNumeroPainel),
                    Face = entidade.Get<int>(CampoFace),
                    TipoPlaca = entidade.Get<string>(CampoTipoPlaca) ?? string.Empty,
                    Orientacao = entidade.Get<string>(CampoOrientacao) ?? string.Empty
                };
                return true;
            }
            catch
            {
                identidade = null;
                return false;
            }
        }
    }
}
