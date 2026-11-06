# TKCode.Arc components

Every component and block of TKCode.Arc 0.3.2-preview.1, by group: its page, its class and what it is for. Read the page of
each one you use: it says when not to use the component and what fits better, shows usage and examples, and lists every
property and event. Components are in the `TKCode.Arc` package unless the line names another one; blocks are in
`TKCode.Arc.Blocks` (not on NuGet yet).

## Components

### Buttons

- [Button](https://docs.tkcode.si/components/button.md): `Button`. A clear, responsive action with quiet secondary states.
- [Action button](https://docs.tkcode.si/components/action-button.md): `ActionButton`. A compact button for frequent toolbar actions.
- [Action swap](https://docs.tkcode.si/components/action-swap.md): `ActionSwap`. Swap an action label while preserving its place and intent.
- [Split button](https://docs.tkcode.si/components/split-button.md): `SplitButton`. A primary action with a menu of nearby alternatives.
- [Button group](https://docs.tkcode.si/components/button-group.md): `ButtonGroup`. Related actions joined into one surface with hairline dividers: a hover highlight glides between segments, the pressed one answers in place, and an attached menu can close the row.
- [Floating button group](https://docs.tkcode.si/components/floating-button-group.md): `FloatingButtonGroup`. Separate soft buttons in a quiet tray, with one shared highlight that morphs from button to button as you move, and a pressed state that settles in place.
- [Expanding button group](https://docs.tkcode.si/components/expanding-button-group.md): `ExpandingButtonGroup`. Icon buttons in a compact group: the one you point at or focus grows to reveal its label while its neighbours slide aside, and an action confirms in place.
- [Copy button](https://docs.tkcode.si/components/copy-button.md): `CopyButton`. Copy a value with immediate confirmation.
- [Icon morph](https://docs.tkcode.si/components/icon-morph.md): `IconMorph`. Icons that morph between states: menu to close, play to pause, copy to check, and more.
- [Confirm morph](https://docs.tkcode.si/components/confirm-morph.md): `ConfirmMorph`. A destructive button that morphs into an inline confirmation, a spinner, and a result with undo.
- [Theme switcher](https://docs.tkcode.si/components/theme-switch.md): `ThemeSwitcher`. Four smooth ways to move between light and dark appearance.

### Gestures

- [Hold to confirm](https://docs.tkcode.si/components/hold-to-confirm.md): `HoldToConfirm`. Confirm a destructive action by holding, not tapping.
- [Swipe actions](https://docs.tkcode.si/components/swipe-actions.md): `SwipeActions`. Reveal row actions with a swipe, or from the same actions in a menu.
- [Reactions](https://docs.tkcode.si/components/reactions.md): `Reactions`. Acknowledge a post in one tap, with counts that roll.

### Menus

- [Dropdown menu](https://docs.tkcode.si/components/dropdown-menu.md): `DropdownMenu`. A focused list of actions anchored to a trigger.
- [Context menu](https://docs.tkcode.si/components/context-menu.md): `ContextMenu`. Secondary actions kept close to the selected object.
- [User menu](https://docs.tkcode.si/components/user-menu.md): `UserMenu`. Your account, settings, theme, and sign out behind the avatar. Opens as a bottom sheet on phones.

### Text fields

- [Input](https://docs.tkcode.si/components/input.md): `Input`. A single line field with clear labels and useful states.
- [Textarea](https://docs.tkcode.si/components/textarea.md): `Textarea`. A multiline field for notes, descriptions, and longer text.
- [Password field](https://docs.tkcode.si/components/password-field.md): `PasswordField`. Capture sensitive text with a visible reveal control.
- [Password strength](https://docs.tkcode.si/components/password-strength.md): `PasswordStrength`. Show how strong a new password is while it is typed.
- [Search field](https://docs.tkcode.si/components/search-field.md): `SearchField`. A recognizable search entry point with clear affordances.
- [Expanding search](https://docs.tkcode.si/components/expanding-search.md): `ExpandingSearch`. An icon that morphs into a search field with results beneath it.
- [Inline edit](https://docs.tkcode.si/components/inline-edit.md): `InlineEdit`. Rename in place: the text becomes a field without moving.

### Special inputs

- [Number field](https://docs.tkcode.si/components/number-field.md): `NumberField`. Enter a bounded number with clear increment controls.
- [Money input](https://docs.tkcode.si/components/money-input.md): `MoneyInput`. A currency field with live grouping, stable width, rolling digits, and minor-unit output.
- [Phone input](https://docs.tkcode.si/components/phone-input.md): `PhoneInput`. A phone field with a country picker, formatting as you type, and E.164 output.
- [Scrub input](https://docs.tkcode.si/components/scrub-input.md): `ScrubInput`. A Figma-style number field: drag the label to scrub, or type values, units, and arithmetic.
- [Tag input](https://docs.tkcode.si/components/tag-input.md): `TagInput`. Turn short text values into removable tags.
- [Mention input](https://docs.tkcode.si/components/mention-input.md): `MentionInput`. A textarea with @people and
- [Shortcut recorder](https://docs.tkcode.si/components/shortcut-recorder.md): `ShortcutRecorder`. Record key combinations into key caps, with conflict warnings, Kbd, and a searchable cheatsheet.

### Selects

- [Select](https://docs.tkcode.si/components/select.md): `Select`. A compact choice field with a keyboard friendly menu.
- [Combobox](https://docs.tkcode.si/components/combobox.md): `Combobox`. Search and select from a list without leaving the field.
- [Multi-select](https://docs.tkcode.si/components/multi-select.md): `MultiSelect`. Select several values while keeping the field readable.
- [Morph select](https://docs.tkcode.si/components/morph-select.md): `MorphSelect`. A select whose trigger grows into the list, with a gliding highlight and type-ahead.
- [Chip group](https://docs.tkcode.si/components/chip-group.md): `ChipGroup`. Filter by a few facets with chips that morph as you pick them.

### Toggles

- [Radio cards](https://docs.tkcode.si/components/radio-cards.md): `RadioCards`. Selectable option cards with a sliding selection ring, price and description slots, and radio keyboard behavior.
- [Billing toggle](https://docs.tkcode.si/components/billing-toggle.md): `BillingToggle`. A monthly and yearly switch with a savings badge and prices that roll to the new amount.
- [Checkbox](https://docs.tkcode.si/components/checkbox.md): `Checkbox`. A binary choice with a precise, legible state.
- [Radio group](https://docs.tkcode.si/components/radio-group.md): `RadioGroup`. Choose one option from a visible set.
- [Switch](https://docs.tkcode.si/components/switch.md): `Switch`. A tactile toggle for settings that take effect immediately.
- [Segmented control](https://docs.tkcode.si/components/segmented-control.md): `SegmentedControl`. Switch between a small set of related views.

### Sliders

- [Slider](https://docs.tkcode.si/components/slider.md): `Slider`. Pick a value or a range on a track that follows your finger.
- [Elastic slider](https://docs.tkcode.si/components/elastic-slider.md): `ElasticSlider`. A slider that stretches like rubber past its ends, with a swaying value tag and detents the thumb clicks into.

### Pickers

- [Calendar](https://docs.tkcode.si/components/calendar.md): `Calendar`. Browse dates in a clear, compact month view.
- [Date picker](https://docs.tkcode.si/components/date-picker.md): `DatePicker`. Choose a date without losing context.
- [Date range picker](https://docs.tkcode.si/components/date-range-picker.md): `DateRangePicker`. A range picker that grows from its trigger into two months with presets and a stretching range highlight.
- [Time picker](https://docs.tkcode.si/components/time-picker.md): `TimePicker`. Choose a time with sensible keyboard behavior.
- [Color picker](https://docs.tkcode.si/components/color-picker.md): `ColorPicker`. A swatch that grows into a picker with format morphing, eyedropper, saved swatches, and contrast readout.

### Editors

- [Rich text editor](https://docs.tkcode.si/components/rich-text-editor.md): `RichTextEditor`. A lightweight editor with markdown shortcuts, a floating toolbar, a slash menu, and HTML and markdown output.
- [Signature pad](https://docs.tkcode.si/components/signature-pad.md): `SignaturePad`. Smooth ink that thins with speed, with undo, replay, and PNG or SVG export.
- [File dropzone](https://docs.tkcode.si/components/file-dropzone.md): `FileDropzone`. A generous target for dropping one or more files.

### Navigation

- [Tabs](https://docs.tkcode.si/components/tabs.md): `Tabs`. Switch between related content in the same context.
- [Breadcrumb](https://docs.tkcode.si/components/breadcrumb.md): `Breadcrumb`. Show where a page sits in a hierarchy.
- [Pagination](https://docs.tkcode.si/components/pagination.md): `Pagination`. Move through a long collection with clear bounds.

### Expand

- [Scroll area](https://docs.tkcode.si/components/scroll-area.md): `ScrollArea`. A native scroll container with thin overlay scrollbars and edge fades that appear only when content overflows.
- [Accordion](https://docs.tkcode.si/components/accordion.md): `Accordion`. Progressively reveal supporting information in place.
- [Expandable card](https://docs.tkcode.si/components/expandable-card.md): `ExpandableCard`. Give a dense card more room when requested.
- [Resizable panels](https://docs.tkcode.si/components/resizable-panels.md): `ResizablePanels`. Trade space between panes by dragging the divider between them.
- [Expandable text](https://docs.tkcode.si/components/expandable-text.md): `ExpandableText`. Long text clamped to a few lines with a soft fade that glides open with Read more and closes with Show less, shown only when the text overflows.

### Overlays

- [Dialog](https://docs.tkcode.si/components/dialog.md): `Dialog`. A focused surface for decisions that need attention.
- [Drawer](https://docs.tkcode.si/components/drawer.md): `Drawer`. A temporary side surface for focused work.
- [Bottom sheet](https://docs.tkcode.si/components/bottom-sheet.md): `BottomSheet`. A sheet that rests at a peek or full height and follows your finger.
- [Popover](https://docs.tkcode.si/components/popover.md): `Popover`. A small anchored surface for contextual information.
- [Hover card](https://docs.tkcode.si/components/hover-card.md): `HoverCard`. Preview a person or link on hover or focus without leaving the page.
- [Tooltip](https://docs.tkcode.si/components/tooltip.md): `Tooltip`. Short supporting text for unfamiliar controls.

### Messages

- [Alert](https://docs.tkcode.si/components/alert.md): `Alert`. A persistent message that helps people recover or continue.
- [Toast](https://docs.tkcode.si/components/toast.md): `Toast`. Brief confirmation for a completed background action.
- [Toast stack](https://docs.tkcode.si/components/toast-stack.md): `ToastStack`. Stack short results at the edge until you reach for them.
- [Announcement bar](https://docs.tkcode.si/components/announcement-bar.md): `AnnouncementBar`. A top banner that rotates messages, counts down, and collapses smoothly when dismissed.
- [Cookie consent](https://docs.tkcode.si/components/cookie-consent.md): `CookieConsent`. A consent bar that morphs into preferences and folds into a reopen button.

### Progress

- [Progress](https://docs.tkcode.si/components/progress.md): `Progress`. Show how much of a known task is complete.
- [Skeleton](https://docs.tkcode.si/components/skeleton.md): `Skeleton`. Reserve space while content is still loading.
- [Stepper](https://docs.tkcode.si/components/stepper.md): `Stepper`. Show where a person is in a multi-step flow and what is done.
- [Countdown](https://docs.tkcode.si/components/countdown.md): `Countdown`. A launch countdown with rolling digits that morphs into a live state at zero.
- [Usage meter](https://docs.tkcode.si/components/usage-meter.md): `UsageMeter`. Show what fills an allowance and how close it is to the limit.

### Avatars

- [Avatar](https://docs.tkcode.si/components/avatar.md): `Avatar`. A compact identity marker for people and accounts.
- [Avatar group](https://docs.tkcode.si/components/avatar-group.md): `AvatarGroup`. Show a team or set of contributors in a small space.
- [Badge](https://docs.tkcode.si/components/badge.md): `Badge`. A small label for status, category, or metadata.

### Cards

- [Card](https://docs.tkcode.si/components/card.md): `Card`. A contained group of related content and actions.
- [Metric card](https://docs.tkcode.si/components/metric-card.md): `MetricCard`. A compact summary for a number that needs context.
- [Empty state](https://docs.tkcode.si/components/empty-state.md): `EmptyState`. A useful next step when there is nothing to show yet.

### Charts

- [Line chart](https://docs.tkcode.si/components/line-chart.md): `LineChart`. A multi-series line chart with a gliding crosshair, legend toggles, and paths that morph between ranges.
- [Bar chart](https://docs.tkcode.si/components/bar-chart.md): `BarChart`. Compare one measure across days and scrub any bar for its value.
- [Donut chart](https://docs.tkcode.si/components/donut-chart.md): `DonutChart`. A donut whose arcs morph between datasets, with the active value rolling into the center.
- [Streamgraph](https://docs.tkcode.si/components/streamgraph.md): `Streamgraph`. Layered streams on a wiggle baseline that morph between ranges, with a layer you can isolate and read week by week.
- [Brush chart](https://docs.tkcode.si/components/brush-chart.md): `BrushChart`. A dense time series with an overview strip: drag a window to zoom, resize it by its handles, and read events in place.
- [Waffle chart](https://docs.tkcode.si/components/waffle-chart.md): `WaffleChart`. A ten by ten unit chart where every cell is one percent, and cells fly to their new group when the data changes.
- [Slope chart](https://docs.tkcode.si/components/slope-chart.md): `SlopeChart`. Before and after on two axes: lines draw in, rank moves sit beside each value, and switching datasets slides every line to its new slope.
- [Sparkline](https://docs.tkcode.si/components/sparkline.md): `Sparkline`. Show a compact trend beside a value.
- [Gauge](https://docs.tkcode.si/components/gauge.md): `Gauge`. Show a value against a known range.
- [Activity heatmap](https://docs.tkcode.si/components/activity-heatmap.md): `ActivityHeatmap`. See a year of activity at a glance, one square per day.
- [Animated counter](https://docs.tkcode.si/components/animated-counter.md): `AnimatedCounter`. Give changing totals a clear sense of movement.
- [Ridgeline](https://docs.tkcode.si/components/ridgeline.md): `Ridgeline`. Overlapping distributions, one ridge per group: hover to lift a ridge and read its quartiles, switch datasets and every curve morphs.
- [Treemap](https://docs.tkcode.si/components/treemap.md): `Treemap`. A squarified treemap: click to drill and the tiles grow to fill the view, with a breadcrumb back and metrics that morph every tile.

### Advanced charts

- [Data grid](https://docs.tkcode.si/components/data-grid.md): `DataGrid`. A spreadsheet grid with range selection, inline editing, a fill handle, clipboard, undo, sorting, filters, grouping, totals and export.

### Workflow

- [Workflow canvas](https://docs.tkcode.si/components/workflow-canvas.md): `WorkflowCanvas`. Show a workflow on a dotted canvas and play its runs: each step lights up in turn, the data draws along the edges, and every step reports its result and time.

### Tables

- [Table](https://docs.tkcode.si/components/table.md): `Table`. Compare structured records with sortable columns.
- [Tree table](https://docs.tkcode.si/components/tree-table.md): `TreeTable`. A table with nested rows, tri-state selection, hierarchy-preserving sort, and lazy-loaded children.
- [Tree view](https://docs.tkcode.si/components/tree-view.md): `TreeView`. Navigate nested folders and structured content.
- [Reorderable list](https://docs.tkcode.si/components/reorderable-list.md): `ReorderableList`. Organize items with direct drag and drop.
- [Filter toolbar](https://docs.tkcode.si/components/filter-toolbar.md): `FilterToolbar`. Keep collection filters close and easy to reset.
- [Code block](https://docs.tkcode.si/components/code-block.md): `CodeBlock`. Present code with legible hierarchy and copy access.
- [JSON viewer](https://docs.tkcode.si/components/json-viewer.md): `JsonViewer`. A collapsible JSON tree with search, paging for long arrays, and copy value or path.

### Activity

- [Timeline](https://docs.tkcode.si/components/timeline.md): `Timeline`. Follow what happened, newest first, grouped by day.
- [Comment thread](https://docs.tkcode.si/components/comment-thread.md): `CommentThread`. Threaded comments with replies, reactions, mentions, inline edit, and resolve.
- [Chat thread](https://docs.tkcode.si/components/chat-thread.md): `ChatThread`. A chat thread with grouped messages, reactions, read receipts, typing, and a composer with attachments.

### Media

- [Image compare](https://docs.tkcode.si/components/image-compare.md): `ImageCompare`. Drag a divider across two images to see what changed.
- [Carousel](https://docs.tkcode.si/components/carousel.md): `Carousel`. Browse a row of slides by dragging, flicking, or arrowing through them.
- [Card stack](https://docs.tkcode.si/components/card-stack.md): `CardStack`. Review a deck one card at a time, with a throw and an undo.

### Text effects

- [Text reveal](https://docs.tkcode.si/components/text-reveal.md): `TextReveal`. Reveal a short piece of content with restrained motion.
- [In-view title](https://docs.tkcode.si/components/in-view-title.md): `InViewTitle`. Bring a section title in as it scrolls into view.
- [Text morph](https://docs.tkcode.si/components/text-morph.md): `TextMorph`. Morph a label into its next state, letter by letter.
- [Text shimmer](https://docs.tkcode.si/components/text-shimmer.md): `TextShimmer`. Show ongoing work with a calm light across the words.

### Extras

- [Markdown](https://docs.tkcode.si/components/markdown.md): `Markdown`. Render Markdown documents and streamed replies in Arc's style, with fenced code highlighted in Arc's syntax palette.
- [Token text editor](https://docs.tkcode.si/components/token-text-editor.md): `TokenTextEditor`. A multi-line text editor whose tokens, such as commands and mentions, behave as single units, with triggers that open a query for your own list.
- [Cascader](https://docs.tkcode.si/components/cascader.md): `Cascader`. Pick one node of a hierarchy level by level, such as province, city and district, with a field and menu that match Select.

### Cursor

- [Dot grid](https://docs.tkcode.si/components/dot-grid.md): `DotGrid`. A field of dots that swells away from the pointer like a lens and sends a ripple out from every click.
- [Gravity well](https://docs.tkcode.si/components/gravity-well.md): `GravityWell`. Tiles that lean toward the pointer, shrink and dim as if pulled into a well, with a click that rolls a shockwave through the grid.
- [Ink fluid](https://docs.tkcode.si/components/ink-fluid.md): `InkFluid`. A fluid simulation behind your content: the pointer stirs soft ink that swirls and fades, and a slow stir keeps it moving when nobody does.

### AI

- [Agent status](https://docs.tkcode.si/components/agent-status.md): `AgentStatus`. A live status pill for a running agent: its mark in a spinning ring, the current step and a timer, opening into the recent steps with pause, stop and approval controls.
- [Tool call](https://docs.tkcode.si/components/tool-call.md): `ToolCall`. A tool call row for an agent transcript: the tool, what it did and how it went, opening into the call's input and output, with approval and retry in place.
- [Agent plan](https://docs.tkcode.si/components/agent-plan.md): `AgentPlan`. An agent's plan as an editable checklist: reorder, rename and add steps while it is a draft, then follow the run step by step once it is approved.
- [Agent timeline](https://docs.tkcode.si/components/agent-timeline.md): `AgentTimeline`. A replay of a multi-agent run on one time axis: each agent's thinking, tool calls and waiting, the handoffs between them, and the time, tokens and cost.
- [Context window](https://docs.tkcode.si/components/context-window.md): `ContextWindow`. A model's context window as a grid of 200 cells that fills part by part, with a compaction threshold, a legend and actions that show what still fits.

### Illustrations

- [Data flow](https://docs.tkcode.si/components/data-flow.md): `DataFlow`. Events stream from their sources through a queue and a batch transform into a warehouse and live dashboards.
- [Payment flow](https://docs.tkcode.si/components/payment-flow.md): `PaymentFlow`. A card payment from checkout through tokenization, authorization and the issuer's decision to settlement.
- [Multi-region failover](https://docs.tkcode.si/components/multi-region-failover.md): `MultiRegionFailover`. A global load balancer shifts traffic to a standby region and promotes its database when the primary region goes down.
- [Vector search](https://docs.tkcode.si/components/vector-search.md): `VectorSearch`. A question is embedded as a vector and answered with its nearest documents by cosine similarity.
- [Agent loop](https://docs.tkcode.si/components/agent-loop.md): `AgentLoop`. An agent gathers context, calls tools and checks the result, going round the loop until the task is done.
- [Mixture of experts](https://docs.tkcode.si/components/mixture-of-experts.md): `MixtureOfExperts`. A router scores every expert for each token and sends it to its top choices, whose outputs blend into the result.
- [Speculative decoding](https://docs.tkcode.si/components/speculative-decoding.md): `SpeculativeDecoding`. A small draft model guesses a few tokens ahead and the large model checks them in one pass, keeping the ones it agrees with.
- [Prompt cache](https://docs.tkcode.si/components/prompt-cache.md): `PromptCache`. Requests that share a prompt prefix read it from the cache instead of processing it again, and the savings add up.
- [Reasoning tree](https://docs.tkcode.si/components/reasoning-tree.md): `ReasoningTree`. A search over candidate thoughts: each is scored, weak branches are pruned, the best expand and the winning path draws in.

### Mobile

- [Mobile page](https://docs.tkcode.si/components/mobile-page.md): `MobilePage`. A phone screen: a header, the content and a footer laid out inside the device's safe area.
- [Mobile navbar](https://docs.tkcode.si/components/mobile-navbar.md): `MobileNavbar`. The 44 px top bar of a page: a leading slot, a centered title and a trailing slot for actions.
- [Mobile toolbar](https://docs.tkcode.si/components/mobile-toolbar.md): `MobileToolbar`. The 44 px bottom action bar of a page: plain actions that share the width, or leading, center and trailing slots.
- [Mobile tabbar](https://docs.tkcode.si/components/mobile-tabbar.md): `MobileTabbar`. The bottom tab bar of a page: icon-over-label tabs that share the width, with the selected one highlighted.
- [Mobile navigation stack](https://docs.tkcode.si/components/mobile-navigation-stack.md): `MobileNavigationStack`. A stack of pages: a pushed page slides in over the one below, and a swipe from the left edge or a back button slides it away.
- [Mobile list](https://docs.tkcode.si/components/mobile-list.md): `MobileListGroup`. Grouped rows for settings and menus: icon, title, subtitle, a value or control at the end and a chevron.
- [Mobile pull to refresh](https://docs.tkcode.si/components/mobile-pull-to-refresh.md): `MobilePullToRefresh`. Pull scrollable content down at its top to refresh it, with rubber-band resistance, a spinner and a spring back.
- [Mobile dialog](https://docs.tkcode.si/components/mobile-dialog.md): `MobileDialog`. A centered alert or confirmation dialog with a title, a message and up to three actions, over a scrim.
- [Mobile toast](https://docs.tkcode.si/components/mobile-toast.md): `MobileToast`. A transient message that slides up near the bottom, leaves by itself and can be swiped away.
- [Mobile sheet](https://docs.tkcode.si/components/mobile-sheet.md): `MobileSheet`. A bottom sheet that rises over a page with a scrim and can rest at several heights, closed by tapping the scrim, pulling the handle down or flicking it.
- [Mobile action sheet](https://docs.tkcode.si/components/mobile-action-sheet.md): `MobileActionSheet`. A bottom list of actions with an optional destructive row and a separate Cancel row, closed by choosing a row, tapping the scrim or pulling it down.
- [Mobile button](https://docs.tkcode.si/components/mobile-button.md): `MobileButton`. A touch-first button with a press state instead of a hover state and a 44 px minimum touch target.
- [Mobile switch](https://docs.tkcode.si/components/mobile-switch.md): `MobileSwitch`. A 51 by 31 toggle for settings that take effect at once, with a spring thumb and a 44 px hit area.
- [Mobile text field](https://docs.tkcode.si/components/mobile-text-field.md): `MobileTextField`. A 44 px filled text field with an optional icon and a clear button, whose border takes the accent color while focused.
- [Mobile search bar](https://docs.tkcode.si/components/mobile-searchbar.md): `MobileSearchbar`. A pill-shaped text field with a search glyph, a clear button and a Search key that raises Submitted.
- [Mobile checkbox](https://docs.tkcode.si/components/mobile-checkbox.md): `MobileCheckbox`. A 22 px rounded checkbox in a 44 px row, with a check, an indeterminate dash and no hover state.
- [Mobile radio](https://docs.tkcode.si/components/mobile-radio.md): `MobileRadio`. A 22 px radio in a 44 px row whose accent dot springs in, one choice per group, with arrow-key navigation and no hover state.
- [Mobile icon button](https://docs.tkcode.si/components/mobile-icon-button.md): `MobileIconButton`. A 44 by 44 icon-only button with plain, tinted, filled and floating variants and a spring press state.
- [Mobile segmented control](https://docs.tkcode.si/components/mobile-segmented.md): `MobileSegmented`. A short row of segments that share the width, one selected, for switching between views of the same content.

## Blocks

### Analytics

- [Metrics dashboard](https://docs.tkcode.si/blocks/metrics-dashboard.md): `MetricsDashboard`. A one-screen analytics overview with a range switch, metric tabs that count to their totals, an inspectable area chart that can overlay the previous period, and two ranked lists.
- [KPI drilldown](https://docs.tkcode.si/blocks/kpi-drilldown.md): `KpiDrilldown`. KPI cards with sparklines and period changes that grow in place into a detail view with a trend chart, a prior-period comparison, a readout you can drive by keyboard and a breakdown by dimension.
- [Metric explorer](https://docs.tkcode.si/blocks/metric-explorer.md): `MetricExplorer`. A row of KPI cards that each open into a chart you can inspect by dragging: the card's surface grows into the panel, the tabs morph the line from metric to metric point by point, the range switch moves a camera window over the daily data, and this period is drawn against the one before it.
- [Revenue explorer](https://docs.tkcode.si/blocks/revenue-explorer.md): `RevenueExplorer`. Monthly recurring revenue as stacked new, expansion and churn layers with three headline numbers and an overview strip: drag its window or handles and the paths, axes and numbers slide to the new range, with range presets, series chips, a previous-period comparison and a crosshair readout for the pointer and the keyboard.
- [MRR waterfall](https://docs.tkcode.si/blocks/mrr-waterfall.md): `MrrWaterfall`. A recurring-revenue bridge from the start of a period to its end: floating bars for new business, expansion, reactivation, contraction and churn, a side panel that lists the accounts behind any bar, and period and unit switches that move every bar, tick and number on one spring.
- [Cohort retention](https://docs.tkcode.si/blocks/cohort-retention.md): `CohortRetention`. How many of each starting group stayed, month by month: a heatmap triangle that morphs into retention curves, headline numbers that compare the newest cohorts with the oldest, segments that repaint every cell, and a cell cursor for the pointer and the keyboard.
- [Usage forecast](https://docs.tkcode.si/blocks/usage-forecast.md): `UsageForecast`. A billing cycle against a budget: measured usage and a dashed forecast with its likely range, a budget line you drag or step with the keys while the headline, tiles, red overage and alert chips react, and a metric switch that morphs every path.
- [Journey flow](https://docs.tkcode.si/blocks/journey-flow.md): `JourneyFlow`. A journey as a Sankey chart: point at a step or a flow to trace that cohort through every stage, select a step to see where its people came from and went next, and switch segments to watch every flow morph.
- [Uptime status](https://docs.tkcode.si/blocks/uptime-status.md): `UptimeStatus`. A status page for a set of services: 90 days of daily bars with uptime figures and a hover card per day, a response-time chart with p50 and p95 curves against a target for the chosen service, and an incident timeline, all derived from the services' samples and the incidents, with keyboard paging by a week.
- [Choropleth explorer](https://docs.tkcode.si/blocks/choropleth-explorer.md): `ChoroplethExplorer`. A map shaded by a metric in quantile steps with a linked ranked list; choose a region to fly into it and rank its counties, switch the metric to sweep new colours across the map, or change the country.
- [Connected scatter](https://docs.tkcode.si/blocks/connected-scatter.md): `ConnectedScatter`. Two measures that move together, drawn as one path: a head point you scrub or play along the months, readouts that count with it, a target line that tints the healthy side, and a switch that morphs the whole path from one series to another.
- [Horizon chart](https://docs.tkcode.si/blocks/horizon-chart.md): `HorizonChart`. Many daily series compared by how far each strays from its own baseline, folded into one thin band row each: the band count folds and unfolds every row on one spring, rows re-sort by name, peak or latest value, and one crosshair reads the same day across every row for the pointer, touch and the keyboard.

### Finance

- [Market terminal](https://docs.tkcode.si/blocks/market-terminal.md): `MarketTerminal`. A trading screen with a candlestick chart you zoom and pan, a live watchlist with sparklines and flashing prices, and a market and limit order ticket with its order list.
- [Budget variance](https://docs.tkcode.si/blocks/budget-variance.md): `BudgetVariance`. Budget against actual spend by department as bullet charts on one shared scale, with signed variances, a period switch that springs every bar, budget lines that open in place and a total row.

### Line charts

- [Small multiples](https://docs.tkcode.si/blocks/small-multiples.md): `SmallMultiples`. A grid of small line charts, one per group, read as a single picture: one crosshair moves through every panel, the y axes switch between each group's own range and a common one, panels re-sort by value, growth or name, and flat groups can be faded.
- [Line replay](https://docs.tkcode.si/blocks/line-replay.md): `LineReplay`. Watch a day of data unfold: the line draws itself as a clock runs, events you mark pop up on the chart when it reaches them, and you can pause, drag to any moment or change the speed.

### AI

- [Model picker](https://docs.tkcode.si/blocks/model-picker.md): `ModelPicker`. Choose a model and its reasoning effort from one compact pill: featured models with a note each, an effort setting, every other model by provider, and a summary of what the choice costs.
- [AI side panel](https://docs.tkcode.si/blocks/ai-side-panel.md): `AiSidePanel`. An assistant beside a page the person reads: it reads the whole page, answers about it or about a selection, and proposes changes as a word diff that the person applies, discards or undoes, with a sweep on the page and an Undo pill.
- [Prompt studio](https://docs.tkcode.si/blocks/prompt-studio.md): `PromptStudio`. A card to edit a prompt template with colored variable chips, try it on saved test cases, and compare and ship versions: version and model menus, variable fields, a Run button and an output panel for the reply.
- [Document extraction](https://docs.tkcode.si/blocks/document-extraction.md): `DocumentExtraction`. Turns a document into typed data and shows where each value came from: a quiet document viewer beside an editable form whose fields fill in as the model reads, with tinted source text, curved links, low-confidence review and a typed JSON result.
- [Model arena](https://docs.tkcode.si/blocks/model-arena.md): `ModelArena`. A blind test of two models: one prompt goes to two anonymous models, both answers stream in at the same pace, and the models are revealed after the vote, with time to first token, speed and cost per answer and a standings list.
- [AI answer](https://docs.tkcode.si/blocks/ai-answer.md): `AiAnswer`. A page that answers one question like an article: reasoning, source cards, a streamed answer with headings, lists, a table, code and numbered citations that light the sentence they support, then actions, related questions and a follow-up box.
- [AI chat](https://docs.tkcode.si/blocks/ai-chat.md): `AiChat`. A chat with an assistant that fills its container: a title menu, a model button and a new chat button on top, a thread with streamed Markdown replies, collapsible reasoning, tool chips, numbered citations and message actions, and a message box at the bottom.
- [AI composer](https://docs.tkcode.si/blocks/ai-composer.md): `AiComposer`. The prompt box of an AI app: a text area that grows, slash commands and @ mentions that become tokens, attachments that upload in place, tool toggles, a model and effort menu, a character counter and a send button that becomes a stop button.
- [Agent memory](https://docs.tkcode.si/blocks/agent-memory.md): `AgentMemory`. A card that shows what an assistant remembers: filter by group, search, edit a sentence in place, forget with a few seconds to undo, file a just-noted memory and add one by hand.

### App shell

- [Sidebar](https://docs.tkcode.si/blocks/sidebar.md): `Sidebar`. Workspace navigation with sections, nested pages, counts and a gliding selection marker; collapses to icons.
- [Page header](https://docs.tkcode.si/blocks/page-header.md): `PageHeader`. A project page header that folds into a compact bar as you scroll, with tabs whose counts roll.
- [Command palette](https://docs.tkcode.si/blocks/command-palette.md): `CommandPalette`. A complete keyboard driven action surface with search, grouped results, and shortcuts.
- [Notification center](https://docs.tkcode.si/blocks/notification-center.md): `NotificationCenter`. A home for updates with read state, grouped information, and animated disclosure.
- [Empty states](https://docs.tkcode.si/blocks/empty-states.md): `EmptyStates`. Four empty states in one illustration whose shapes morph between scenes as you switch tabs.
- [Search results](https://docs.tkcode.si/blocks/search-results.md): `SearchResults`. A workspace search page with a suggestions popup, filters whose counts follow the query, a date range, sorting, removable filter chips, highlighted results and a detail sheet.

### Auth

- [Login](https://docs.tkcode.si/blocks/login.md): `Login`. Sign in and sign up pages, from a compact code card to full screen split and immersive layouts.
- [Onboarding illustration](https://docs.tkcode.si/blocks/onboarding-illustration.md): `OnboardingIllustration`. A compact first-run card whose line drawing morphs from an empty box to files, a team and a badge as people step through it.
- [OTP input](https://docs.tkcode.si/blocks/otp-input.md): `OtpInput`. A six digit verification flow with paste support and keyboard navigation.
- [Multi-step form](https://docs.tkcode.si/blocks/multi-step-form.md): `MultiStepForm`. A short form asked one step at a time in a card, with a progress list, Back and Continue, checks before each step, and a success state at the end.

### Settings

- [Settings page](https://docs.tkcode.si/blocks/settings-page.md): `SettingsPage`. A full settings page: section navigation with an account card, the chosen section beside it, and a save bar for unsaved changes.
- [Settings command](https://docs.tkcode.si/blocks/settings-command.md): `SettingsCommand`. Find any setting from one search field and change it in place: toggles flip, choices cycle and links open, all from the keyboard.
- [Security settings](https://docs.tkcode.si/blocks/security-settings.md): `SecuritySettings`. A security checkup, password change, two-step verification and signed-in sessions, each handled in place.
- [Billing overview](https://docs.tkcode.si/blocks/billing-overview.md): `BillingOverview`. The current plan, next charge, payment method and invoice history on one page, with the actions next to each.
- [Usage billing](https://docs.tkcode.si/blocks/usage-billing.md): `UsageBilling`. A metered billing period: spend so far, a projected total, daily spend, overage per meter and a spending limit.
- [Cancel flow](https://docs.tkcode.si/blocks/cancel-flow.md): `CancelFlow`. A considerate cancellation: ask why, offer a fitting alternative, show what ends, hold to confirm, and allow a quick return.

### People

- [Team members](https://docs.tkcode.si/blocks/team-members.md): `TeamMembers`. The members page of a workspace: invite by typing or pasting addresses that fly into pending invites as the seat count rolls, change roles, search, and remove people with an inline confirm.
- [Invite people](https://docs.tkcode.si/blocks/invite-people.md): `InvitePeople`. Invite people by name or email into a workspace, with a role for each, a seat meter against the plan, and avatars that fly from the suggestions into chips and on into a pending list.
- [Team directory](https://docs.tkcode.si/blocks/team-directory.md): `TeamDirectory`. A company directory with search, team filters and the picked person's profile beside the list, from bio and status to an email address to copy.
- [Roles and permissions](https://docs.tkcode.si/blocks/roles-permissions.md): `RolesPermissions`. A matrix of roles and permissions with group checkboxes that ripple, locked owner access, role copies and a review bar that saves every change at once.
- [Profile page](https://docs.tkcode.si/blocks/profile-page.md): `ProfilePage`. A person's profile as one page: a cover and avatar, an about column with stats, Activity and Projects tabs, and an edit sheet whose saved fields glow once.

### Developer

- [API keys](https://docs.tkcode.si/blocks/api-keys.md): `ApiKeys`. API key management where a Create key pill unfolds into a form, the new secret is shown once, rows open into usage details and a hold revokes a key.
- [Webhooks](https://docs.tkcode.si/blocks/webhooks.md): `Webhooks`. A webhooks console where you check each endpoint's health and traffic, inspect what was sent and what came back, retry failed deliveries in place and add endpoints with a one-time signing secret.
- [Integrations](https://docs.tkcode.si/blocks/integrations.md): `Integrations`. A directory of apps to connect, with search, counted category filters, a connect pill that spins, connects or offers a retry, and a drawer for permissions, sync frequency and hold to disconnect.
- [Env variables](https://docs.tkcode.si/blocks/env-variables.md): `EnvVariables`. A project's environment variables as a matrix of switches per environment, with masked values, inline editing, .env import, a fingerprint comparison and a redeploy banner.

### Media

- [File upload](https://docs.tkcode.si/blocks/file-upload.md): `FileUpload`. A complete file selection flow with constraints, progress, and error feedback.
- [Media player](https://docs.tkcode.si/blocks/media-player.md): `MediaPlayer`. An audio player with cover art, a playlist, a seek bar, previous and next, and volume with mute; skipping wraps around and keeps playing or paused, and the playing row shows moving bars.

### Projects

- [Week calendar](https://docs.tkcode.si/blocks/week-calendar.md): `WeekCalendar`. A week planner where you drag to add, move and resize events and undo the last change, with a month and calendar toggles beside the grid, an all-day row, and three-day or agenda views when narrow.
- [Availability picker](https://docs.tkcode.si/blocks/availability-picker.md): `AvailabilityPicker`. Find a meeting time across several people's calendars, see who is free at each time, and book the one that works.
- [Project board](https://docs.tkcode.si/blocks/project-board.md): `ProjectBoard`. A compact sprint board: arrow buttons step each card to the next or previous stage and it flies to its new column, under a sprint line, the team and an All work / My work switch.
- [Release readiness](https://docs.tkcode.si/blocks/release-readiness.md): `ReleaseReadiness`. A release gate where a short list of checks unlocks one action, with a progress bar, the owners, a notify switch and the result in place.
- [Kanban board](https://docs.tkcode.si/blocks/kanban-board.md): `KanbanBoard`. A team board whose cards lift, tilt with the pointer and spring into the slot that opens for them, with swimlanes by person, live work-in-progress limits, quick add and a details drawer.

### Headers and footers

- [Site header](https://docs.tkcode.si/blocks/site-header.md): `SiteHeader`. A sticky website header that turns solid on scroll, with a gliding active link, mega menu panels, and a mobile sheet.
- [Site footer](https://docs.tkcode.si/blocks/site-footer.md): `SiteFooter`. A website footer with link columns and newsletter, a minimal layout, and a large fading Arc mark.
- [CTA section](https://docs.tkcode.si/blocks/cta-section.md): `CtaSection`. A call to action as a centered closing section, a split beside a setup card that completes itself, or a dismissible banner.
- [Newsletter signup](https://docs.tkcode.si/blocks/newsletter-signup.md): `NewsletterSignup`. An email signup framed by a stack of past issues; subscribing drops the next issue, addressed to you, onto the front.

### Showpieces

- [Activity terrain](https://docs.tkcode.si/blocks/activity-terrain.md): `ActivityTerrain`. A week of hourly activity as a 3D terrain: orbit and zoom it, hover an hour for its numbers, fly to a bar, press it flat into a heat map, and read the total, the peak hours and the busiest day beside it.
- [Revenue globe](https://docs.tkcode.si/blocks/revenue-globe.md): `RevenueGlobe`. A live payments map: arcs fly from every city to headquarters on a dotted globe, with a day timeline to replay or scrub and the revenue, regions and top cities beside it.
- [Customer galaxy](https://docs.tkcode.si/blocks/customer-galaxy.md): `CustomerGalaxy`. Every customer as a dot sized by revenue and colored by health, turning as a galaxy; group them into clusters, fly in, and read the totals.
- [Semantic zoom](https://docs.tkcode.si/blocks/semantic-zoom.md): `SemanticZoom`. An organization map you zoom through from teams to projects to a single task, each level showing new detail, with breadcrumbs, a minimap, a guided tour and the numbers of whatever is in view.
- [Layout morph](https://docs.tkcode.si/blocks/layout-morph.md): `LayoutMorph`. One set of cards in six views, from a grid and a list to a fanned stack, a 3D ring and a helix, with every card flying to its new place.
- [Exploded view](https://docs.tkcode.si/blocks/exploded-view.md): `ExplodedView`. A screen that comes apart as you scroll: its layers tip back, separate with labels, take turns in focus with a caption each, and close into one screen again.
