# Roadmap

## Implementado

### Hub central

- Launcher `wolfhub.ps1` abre o hub WinForms; `-Console` mantém a TUI PowerShell disponível.
- Catálogo de módulos separado do código de UI em `config/modules.json`.
- Manifesto de ações em `config/features.json` com efeitos, permissões e limites de reversão, usado nas confirmações.
- Build do hub incorpora catálogos, perfis e o WolfHub Audio standalone para execução fora da árvore fonte.
- Tela local de backup inicial, comparação e restauração dos ajustes compatíveis.

### Instalação e módulos

- Catálogo WinGet central e perfis Minimal, Personal e Support; seleção individual, detecção e confirmação antes de instalar.
- Atualização geral do WinGet com confirmação de escopo.
- Toolkit para abrir ou copiar utilitários locais e criar atalhos.
- WolfHub Audio preservado como módulo WinForms independente, com configuração XML própria e atualização do executável separada.
- Ajustes de energia, opções do Explorador, menu clássico e limpeza de cache DNS, todos identificados no manifesto de ações.
- Pacote de drivers antigo identificado como específico do modelo ASUS B660M TUF, com confirmação contextual antes de abrir o instalador externo.

### WolfHub Audio

- Perfis de saída principal, saída secundária opcional e gravação, teste de endpoint e medidor de entrada.
- Alternância de áudio pela bandeja e configuração global com persistência `%LOCALAPPDATA%\WolfHubAudio\settings.xml`.
- Atualizar o executável do módulo pelo hub não apaga suas preferências XML.

### Validação

- `hub-app/build.ps1` compila hub e módulo de áudio empacotado.
- `WolfHub.exe --self-test` verifica módulos, ações, catálogo, perfis e serialização do backup sem alterações no Windows.
- `tests/Test-WolfHub.ps1` verifica JSON, perfis, IDs, metadados obrigatórios e sintaxe PowerShell.

## Limites conhecidos

- O backup é específico aos três grupos de estado descritos em `docs/ARCHITECTURE.md`; não é imagem do Windows nem ponto de restauração.
- Inventário WinGet no backup cobre apenas os IDs conhecidos do catálogo e é informativo. Não há desinstalação/downgrade automático.
- O instalador de drivers é legado e específico ao hardware identificado; o hub não valida compatibilidade do equipamento.
- Utilitários pessoais binários e payloads de driver continuam fora do versionamento Git; podem não estar disponíveis numa cópia limpa do repositório.
- O visual e o fluxo de áudio dependem de validação manual de hardware, DPI e sinal real em cada computador.

## Próximos passos

- Ampliar o modelo declarativo para incluir dependências, requisitos mínimos do Windows e testes/detecção do estado por ação.
- Registrar resultados de ações e códigos de saída num histórico local legível, sem coletar dados pessoais desnecessários.
- Permitir exportar/importar perfis de preparação do PC sem caminhos locais ou identificadores de máquina.
- Adicionar simulação e reversão documentada antes de implementar Debloat.
- Trocar o caminho de drivers específicos por consulta a fontes oficiais e validação explícita de fabricante/modelo/versão.
- Definir distribuição versionada e validação de integridade antes de disponibilizar bootstrap remoto.
