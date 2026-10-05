# CLAUDE.md - Plugin LSF para Revit

Contexto rapido para retomar este projeto em uma nova conversa. Ver tambem ROADMAP.md (fases) e DECISOES.md (ADRs).

## Ambiente
- Revit 2021 (alvo real, nao 2024). Projeto em C:\lsf_revit_plugin\LSFRevitPlugin
- .NET Framework 4.8, Visual Studio Community 2022 (instalado via winget)
- Ritmo: fechar Revit -> editar codigo -> dotnet build -> reabrir Revit -> testar (Windows trava a DLL com o Revit aberto)
- Paredes de teste precisam de Linha de Localizacao = "Linha de centro da parede"

## Mapa de pastas real
LSFRevitPlugin/
LSFRevitPlugin.addin
Core/
App.cs - aba "LSF Modeler" + botao "Ativar LSF"
Command.cs - ponto de entrada: seleciona parede, abre UI/OpcoesGeracaoWindow, chama Services/StructureGenerationService, mostra TaskDialog
RevitVersionAdapter.cs - conversao de unidades centralizada
UI/
OpcoesGeracaoWindow.xaml(.cs) - janela WPF: escolhe espacamento 40/60cm
Domain/
Models/ - POCOs sem dependencia do Revit API (PerfilLSF, ResultadoVerificacao, WallJunctionInfo, TJunctionInfo, CruzJunctionInfo, OpeningInfo, ElementoReforcoVao, ResultadoGeracao)
Structural/Nbr14762Calculator.cs - calculo estrutural puro, testavel sem Revit
Geometry/
WallFraming.cs - distribuicao linear de montantes
StudSolidBuilder.cs, StudDirectShapeFactory.cs - geometria 3D (caixa simplificada)
WallGeometryHelpers.cs - funcoes compartilhadas (ObterEspessuraNucleo)
LJunctionCalculator.cs - encontro em L (Duplo)
TJunctionCalculator.cs - encontro em T (Triplo)
CruzJunctionCalculator.cs - encontro em + (Quadruplo)
OpeningReinforcement.cs - deteccao de vaos + reforco (King/Jack Studs, verga/contra-verga)
Services/
StructureGenerationService.cs - orquestra tudo: WallFraming + os 3 calculators + OpeningReinforcement, cria os elementos numa unica transacao
Tests/LSFRevitPlugin.Domain.Tests/ - 8 testes xUnit do Nbr14762Calculator

Estado atual (ver ROADMAP.md para detalhe fase a fase)
Fases 1-4 completas: infraestrutura, distribuicao de montantes, calculo estrutural, encontros L/T/+
Fase 5 (vaos) em andamento: deteccao + King/Jack Studs + verga/contra-verga prontos; falta cripple studs (montantes curtos acima/abaixo do vao)
Fase 6 (UI) parcial: so o seletor de espacamento existe
Pendencia imediata

Implementar cripple studs em OpeningReinforcement.cs: montantes curtos seguindo o espacamento normal da parede, no trecho acima da contra-verga (ambos os tipos de vao) e no trecho abaixo da verga/soleira (so janela).

Regra de arquitetura inegociavel

Nada em Domain/ pode referenciar Autodesk.Revit.DB -- e o que garante os testes unitarios do calculo estrutural sem precisar abrir o Revit.
