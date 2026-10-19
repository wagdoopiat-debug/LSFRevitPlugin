using System.Collections.Generic;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeConfiguracaoTextoTests
    {
        private static ContornoLaje Retangulo()
        {
            return new ContornoLaje(new[]
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0),
                new Ponto2DMm(6000, 4000), new Ponto2DMm(0, 4000)
            });
        }

        private static ParametrosLaje Completo()
        {
            return new ParametrosLaje
            {
                Contorno = Retangulo(),
                Tipo = TipoLaje.Seca,
                Sistema = SistemaLaje.VigasLsfComGuiasBorda,
                Perfil = new PerfilSpec
                {
                    Sistema = SistemaPerfil.SteelFrame,
                    LarguraAlmaMm = 140,
                    EspessuraChapaMm = 1.25
                },
                EspacamentoMm = 600,
                CotaMm = 3000,
                EspessuraTelhaMm = TelhaTp40.Espessura043Mm,
                ModoOrientacao = OrientacaoVigasModo.ParalelaAresta,
                ArestaReferencia = 1,
                DirecaoVigasGraus = 90
            };
        }

        [Fact]
        public void Ida_e_volta_preserva_as_escolhas()
        {
            string texto = ConfiguracaoLajeTexto.Serializar(Completo());

            ParametrosLaje lido;
            Assert.True(ConfiguracaoLajeTexto.TentarDeserializar(texto, out lido));

            Assert.Equal(TipoLaje.Seca, lido.Tipo);
            Assert.Equal(SistemaPerfil.SteelFrame, lido.Perfil.Sistema);
            Assert.Equal(140.0, lido.Perfil.LarguraAlmaMm, 6);
            Assert.Equal(1.25, lido.Perfil.EspessuraChapaMm, 6);
            Assert.Equal(600.0, lido.EspacamentoMm, 6);
            Assert.Equal(3000.0, lido.CotaMm, 6);
            Assert.Equal(OrientacaoVigasModo.ParalelaAresta, lido.ModoOrientacao);
            Assert.Equal(1, lido.ArestaReferencia);
            Assert.Equal(90.0, lido.DirecaoVigasGraus.GetValueOrDefault(), 6);
            Assert.Equal(6000.0 * 4000.0, lido.Contorno.AreaMm2, 3);
        }

        [Fact]
        public void Direcao_automatica_volta_como_nula()
        {
            var p = Completo();
            p.ModoOrientacao = OrientacaoVigasModo.Automatica;
            p.DirecaoVigasGraus = null;

            ParametrosLaje lido;
            Assert.True(ConfiguracaoLajeTexto.TentarDeserializar(
                ConfiguracaoLajeTexto.Serializar(p), out lido));
            Assert.Null(lido.DirecaoVigasGraus);
            Assert.Equal(OrientacaoVigasModo.Automatica, lido.ModoOrientacao);
        }

        [Fact]
        public void Texto_invalido_devolve_falso_sem_lancar()
        {
            ParametrosLaje lido;
            Assert.False(ConfiguracaoLajeTexto.TentarDeserializar(null, out lido));
            Assert.False(ConfiguracaoLajeTexto.TentarDeserializar("", out lido));
            Assert.False(ConfiguracaoLajeTexto.TentarDeserializar("versao=99\ncontorno=0,0;1,0;1,1", out lido));
            Assert.False(ConfiguracaoLajeTexto.TentarDeserializar("versao=1", out lido));
        }

        [Fact]
        public void Config_antiga_com_paginar_e_junta_placas_ainda_abre()
        {
            // A paginacao ficou obrigatoria: as chaves antigas "paginar"/"juntaPlacas" sao
            // ignoradas, mas a configuracao antiga deve continuar abrindo sem falha.
            string texto = ConfiguracaoLajeTexto.Serializar(Completo())
                + "\npaginar=0\njuntaPlacas=7.5\n";

            ParametrosLaje lido;
            Assert.True(ConfiguracaoLajeTexto.TentarDeserializar(texto, out lido));
            Assert.Equal(140.0, lido.Perfil.LarguraAlmaMm, 6);
            Assert.Equal(3.0, PaginacaoPlacas.JuntaPadraoMm, 6);
        }
    }

    public class LajeRegistroTextoTests
    {
        private static ParametrosLaje Parametros()
        {
            return new ParametrosLaje
            {
                Contorno = new ContornoLaje(new[]
                {
                    new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0),
                    new Ponto2DMm(6000, 4000), new Ponto2DMm(0, 4000)
                })
            };
        }

        [Fact]
        public void Ida_e_volta_preserva_dois_registros()
        {
            var registros = new List<RegistroLaje>
            {
                new RegistroLaje
                {
                    LajeId = 3,
                    OrigemTipo = TipoOrigemContorno.PisoExistente,
                    OrigemElementoId = 12345,
                    Parametros = Parametros()
                },
                new RegistroLaje
                {
                    LajeId = 7,
                    OrigemTipo = TipoOrigemContorno.PontosDesenhados,
                    OrigemElementoId = -1,
                    Parametros = Parametros()
                }
            };

            string texto = RegistroLajesTexto.Serializar(registros);

            List<RegistroLaje> lidos;
            Assert.True(RegistroLajesTexto.TentarDeserializar(texto, out lidos));
            Assert.Equal(2, lidos.Count);
            Assert.Equal(3, lidos[0].LajeId);
            Assert.Equal(TipoOrigemContorno.PisoExistente, lidos[0].OrigemTipo);
            Assert.Equal(12345, lidos[0].OrigemElementoId);
            Assert.Equal(7, lidos[1].LajeId);
            Assert.Equal(TipoOrigemContorno.PontosDesenhados, lidos[1].OrigemTipo);
            Assert.Equal(-1, lidos[1].OrigemElementoId);
            Assert.Equal(24.0, lidos[1].Parametros.Contorno.AreaM2, 3);
        }

        [Fact]
        public void Registro_vazio_devolve_falso()
        {
            List<RegistroLaje> lidos;
            Assert.False(RegistroLajesTexto.TentarDeserializar(null, out lidos));
            Assert.False(RegistroLajesTexto.TentarDeserializar("qualquer coisa", out lidos));
            Assert.Empty(lidos);
        }

        [Fact]
        public void Bloco_invalido_e_ignorado_mas_os_validos_ficam()
        {
            string texto = RegistroLajesTexto.Serializar(new List<RegistroLaje>
            {
                new RegistroLaje
                {
                    LajeId = 1,
                    OrigemTipo = TipoOrigemContorno.BordasSelecionadas,
                    OrigemElementoId = -1,
                    Parametros = Parametros()
                }
            });

            texto += "\n###\nlajeId=2\norigemTipo=PisoExistente\norigemId=9\nversao=99\n";

            List<RegistroLaje> lidos;
            Assert.True(RegistroLajesTexto.TentarDeserializar(texto, out lidos));
            Assert.Single(lidos);
            Assert.Equal(1, lidos[0].LajeId);
        }
    }
}
