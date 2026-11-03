# WolfHub

WolfHub é um toolkit modular para preparar e manter computadores Windows. O aplicativo central coordena perfis e módulos independentes; cada ação informa o que altera, as permissões esperadas e o que pode ser restaurado.

## Requisitos

- Windows 10 ou 11.
- Windows PowerShell 5.1 ou PowerShell 7.
- .NET Framework 4.x e compilador C# do Windows para gerar o hub e o WolfHub Audio.
- WinGet para instalar e atualizar pacotes; as demais páginas abrem sem WinGet.
- Elevação somente quando o usuário a solicita ou quando um instalador externo a exige.

## Executar

Na pasta do projeto, abra PowerShell e execute:

```powershell
.\wolfhub.ps1
```

O launcher compila o aplicativo central quando necessário e abre o WolfHub desktop. Use `-Console` para abrir a TUI PowerShell:

```powershell
.\wolfhub.ps1 -Console
```

Para compilar manualmente:

```powershell
.\audio-app\build.ps1
.\hub-app\build.ps1
```

O build do hub incorpora seus catálogos e perfis JSON e o executável independente WolfHub Audio. O aplicativo não muda a ExecutionPolicy global.

## Funcionalidades disponíveis

- **Meu Toolkit:** abre ou instala utilitários locais e o WolfHub Audio. O Audio Switcher tem executável, configuração XML e atualização separados; as preferências existentes são mantidas ao atualizar o binário.
- **Pós-Formatação:** seleção individual e por perfil, detecção de pacotes conhecidos, instalação e atualização por WinGet com confirmação.
- **Energia & Tweaks:** planos de energia, opções do Explorador, menu clássico e limpeza de cache DNS. A confirmação explica o efeito e a reversão.
- **Backup do Sistema:** guarda automaticamente, antes do primeiro ajuste compatível, o plano de energia ativo e os estados de registro que o hub altera. O inventário de aplicativos conhecidos é informativo; instalar ou atualizar programas não é revertido automaticamente.
- **WolfHub Audio:** perfis de saída/gravação, teste direcionado por dispositivo, nível de microfone e alternância pela bandeja. Sua configuração fica em `%LOCALAPPDATA%\WolfHubAudio\settings.xml`.

As definições de módulos ficam em [`config/modules.json`](config/modules.json); descrições de efeito, permissão e reversão ficam em [`config/features.json`](config/features.json). Catálogo e perfis são mantidos separadamente em `config/packages.json` e `profiles/*.json`.

## Backup e restauração

O backup inicial local fica em `%LOCALAPPDATA%\WolfHub\backups\baseline.json`, acompanhado da exportação da chave de registro do menu de contexto quando aplicável. A tela mostra diferenças antes de restaurar e guarda uma cópia do estado atual antes da restauração.

Restauração cobre o plano de energia ativo, os valores `HideFileExt`/`Hidden` do usuário atual e a chave usada pelo menu clássico. Ela não desinstala programas, não remove planos adicionais e não restaura drivers. Para reversões de driver, use os mecanismos do Windows ou do fabricante.

## Limitações

- O inventário de WinGet considera somente IDs do catálogo WolfHub e pode depender da versão/idioma do WinGet.
- Drivers legados são específicos da ASUS B660M TUF e continuam fora do fluxo automático; confirme hardware e Windows antes de abrir o instalador.
- Materiais originais em `audio/`, `DriversRafa/` e executáveis portáteis pessoais não são executados automaticamente pelo hub.
- A TUI PowerShell é um modo alternativo; a interface central atual é WinForms/.NET Framework.
- O backup é uma proteção pontual para ajustes conhecidos, não uma imagem completa do sistema nem um substituto de ponto de restauração do Windows.

## Verificação segura

```powershell
.\hub-app\build.ps1
.\hub-app\dist\WolfHub.exe --self-test
.\tests\Test-WolfHub.ps1
```

Essas verificações não instalam pacotes nem aplicam ajustes.

## Estrutura

- `hub-app/`: aplicativo central WinForms e build .NET Framework.
- `audio-app/`: módulo independente WolfHub Audio.
- `src/`: TUI PowerShell e módulo de instalação alternativo.
- `config/modules.json`: módulos, finalidade e localização dos dados.
- `config/features.json`: efeitos, permissões e limites de reversão por ação.
- `config/packages.json`, `profiles/`: catálogo e conjuntos reutilizáveis de IDs.
- `docs/`: arquitetura, inventário de legado e roadmap.
- `tests/`: verificações JSON, perfis, manifestos, caminhos e sintaxe PowerShell.

Consulte [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), [`docs/ROADMAP.md`](docs/ROADMAP.md) e [`audio-app/README.md`](audio-app/README.md).
