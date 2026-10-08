using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Guarda a identidade de um elemento de estrutura gerado -- parede, painel, tipo e perfil --
    /// no proprio elemento, via Extensible Storage. E a fonte de verdade dessa relacao.
    ///
    /// O texto no campo Comentarios continua sendo gravado com os mesmos valores, porque e o
    /// que o usuario le no modelo e o que sustenta os projetos gerados antes deste schema
    /// existir. Os dois canais sao gravados juntos, na mesma chamada, entao nao podem divergir.
    ///
    /// A identidade do painel e o par (ParedeId, NumeroPainel); nao ha id proprio de painel.
    /// Nao e um canal paralelo de identidade: e o mesmo mecanismo que a PainelizacaoService ja
    /// usa na parede, com o mesmo formato (schema com GUID, campos simples).
    ///
    /// Nao implementa nada para placas nem para parafusos ainda: esta etapa cobre apenas os
    /// elementos de estrutura, que sao gravados pelo GenerationService.
    /// </summary>
    public static class PertencenciaPainelService
    {
        private static readonly Guid SchemaGuid = new Guid("2b65139b-add8-449c-95d6-3c7fcdbd8831");
        private const string NomeSchema = "LSFPertencenciaPaineis";
        private const string CampoParedeId = "ParedeId";
        private const string CampoNumeroPainel = "NumeroPainel";
        private const string CampoTipo = "Tipo";
        private const string CampoPerfil = "Perfil";

        private static Schema ObterOuCriarSchema()
        {
            Schema existente = Schema.Lookup(SchemaGuid);
            if (existente != null) return existente;

            var builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName(NomeSchema);
            builder.SetVendorId("LSFModeler");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(CampoParedeId, typeof(int));
            builder.AddSimpleField(CampoNumeroPainel, typeof(int));
            builder.AddSimpleField(CampoTipo, typeof(string));
            builder.AddSimpleField(CampoPerfil, typeof(string));
            return builder.Finish();
        }

        /// <summary>
        /// Grava a identidade no elemento. Precisa ser chamado com uma Transaction aberta.
        /// </summary>
        public static void Gravar(Element elemento, int paredeId, int numeroPainel, string tipo, string perfil)
        {
            if (elemento == null) return;

            var entidade = new Entity(ObterOuCriarSchema());
            entidade.Set(CampoParedeId, paredeId);
            entidade.Set(CampoNumeroPainel, numeroPainel);
            entidade.Set(CampoTipo, tipo ?? "");
            entidade.Set(CampoPerfil, perfil ?? "");
            elemento.SetEntity(entidade);
        }

        /// <summary>
        /// Le a identidade gravada no elemento. Devolve false quando o elemento nao tem esse
        /// schema -- acontece com tudo que foi gerado antes desta etapa existir.
        /// </summary>
        public static bool TentarLer(Element elemento, out IdentidadeEstrutural identidade)
        {
            identidade = null;
            if (elemento == null) return false;

            // Lookup, nao ObterOuCriar: a leitura nao pode criar schema (precisaria de transacao).
            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema == null) return false;

            Entity entidade = elemento.GetEntity(schema);
            if (entidade == null || !entidade.IsValid()) return false;

            try
            {
                identidade = new IdentidadeEstrutural
                {
                    ParedeId = entidade.Get<int>(CampoParedeId),
                    NumeroPainel = entidade.Get<int>(CampoNumeroPainel),
                    Tipo = entidade.Get<string>(CampoTipo),
                    Perfil = entidade.Get<string>(CampoPerfil)
                };
                return true;
            }
            catch
            {
                // Schema gravado por outra versao do plugin, sem estes campos: nao quebra a
                // leitura, o chamador cai no texto de Comentarios.
                identidade = null;
                return false;
            }
        }
    }
}