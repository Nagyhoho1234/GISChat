# Changelog

## [1.1.0] - 2026-03-08

### Added
- **Google Earth Engine integration** -- configure your GEE project in Settings, and ask the AI to query, process, and download GEE data directly into ArcGIS Pro
- Automatic GEE setup: checks for earthengine-api installation and authentication when configuring
- Raster layer context: band names, pixel types, and cell sizes are now sent to the AI
- Multi-tool execution: handles multiple tool calls per response (fixes Anthropic tool_use/tool_result contract)
- Recursive follow-up processing for complex multi-step tasks (up to 10 rounds)
- Conversation history truncation (max 40 messages) with smart tool_use/tool_result boundary handling
- Debug JSONL logging of conversation history (`%APPDATA%/GISChat/logs/conversation_*.jsonl`)
- Rollback error recovery instead of full history clear on tool sync errors

### Fixed
- Critical bug: only the last tool_use block was captured when the AI returned multiple tool calls in one response, causing `tool_use ids without tool_result blocks` errors
- Follow-up responses containing tool calls were silently dropped instead of being processed recursively
- System prompt now prevents the AI from using `subprocess` with `sys.executable` (which launches a new ArcGIS Pro instance instead of running Python)

## [1.0.0] - 2026-03-06

### Added
- AI-powered chat panel docked in ArcGIS Pro
- Natural language GIS task execution via ArcPy code generation
- Map context awareness (layers, fields, extent, spatial reference)
- Multi-provider support: Anthropic (Claude), OpenAI (GPT), Google Gemini, Ollama, OpenAI-compatible
- Settings dialog for provider, model, API key, and endpoint configuration
- Automatic error recovery with alternative approaches
- Confirmation dialog before executing generated code
- Code display toggle in chat
- File-based logging with 14-day rotation
- One-click GitHub issue reporting with pre-filled error details
- Standalone GUI installer with provider setup wizard
- Manual .esriAddinX installation support
