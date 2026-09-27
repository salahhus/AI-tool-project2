"""Generates the Arkik Opportunity Classifier hackathon deck (docs/Arkik-Opportunity-Classifier.pptx).

Run:  python tools/generate_deck.py
"""

from pathlib import Path

from pptx import Presentation
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN
from pptx.util import Emu, Inches, Pt

# Arkik slide palette.
NAVY = RGBColor(0x0B, 0x1B, 0x33)
CARD = RGBColor(0x12, 0x29, 0x4A)
ACCENT = RGBColor(0x4D, 0xA3, 0xFF)
WHITE = RGBColor(0xE8, 0xEE, 0xFC)
MUTED = RGBColor(0x9D, 0xB4, 0xD8)
GREEN = RGBColor(0x3F, 0xC3, 0x80)
YELLOW = RGBColor(0xFF, 0xD5, 0x4A)

SLIDE_W = Inches(13.333)
SLIDE_H = Inches(7.5)


def add_slide(prs):
	slide = prs.slides.add_slide(prs.slide_layouts[6])  # blank
	bg = slide.background.fill
	bg.solid()
	bg.fore_color.rgb = NAVY
	return slide


def add_text(slide, text, left, top, width, height, size, color=WHITE,
			 bold=False, align=PP_ALIGN.LEFT, line_spacing=1.0):
	box = slide.shapes.add_textbox(left, top, width, height)
	frame = box.text_frame
	frame.word_wrap = True
	para = frame.paragraphs[0]
	para.alignment = align
	para.line_spacing = line_spacing
	run = para.add_run()
	run.text = text
	run.font.size = Pt(size)
	run.font.bold = bold
	run.font.color.rgb = color
	run.font.name = "Segoe UI"
	return box


def add_card(slide, left, top, width, height, fill=CARD, line=ACCENT):
	from pptx.enum.shapes import MSO_SHAPE

	shape = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, left, top, width, height)
	shape.fill.solid()
	shape.fill.fore_color.rgb = fill
	shape.line.color.rgb = line
	shape.line.width = Pt(1)
	shape.shadow.inherit = False
	shape.text_frame.word_wrap = True
	return shape


def card_with_lines(slide, left, top, width, height, title, lines,
					title_color=ACCENT, title_size=18, body_size=14):
	shape = add_card(slide, left, top, width, height)
	frame = shape.text_frame
	frame.margin_left = Inches(0.25)
	frame.margin_right = Inches(0.2)
	frame.margin_top = Inches(0.18)

	para = frame.paragraphs[0]
	run = para.add_run()
	run.text = title
	run.font.size = Pt(title_size)
	run.font.bold = True
	run.font.color.rgb = title_color
	run.font.name = "Segoe UI"

	for line in lines:
		p = frame.add_paragraph()
		p.space_before = Pt(7)
		r = p.add_run()
		r.text = line
		r.font.size = Pt(body_size)
		r.font.color.rgb = WHITE
		r.font.name = "Segoe UI"
	return shape


def slide_title(slide, text, subtitle=None):
	add_text(slide, text, Inches(0.7), Inches(0.45), Inches(12), Inches(0.8), 32, WHITE, bold=True)
	if subtitle:
		add_text(slide, subtitle, Inches(0.7), Inches(1.15), Inches(12), Inches(0.5), 15, MUTED)


def build():
	prs = Presentation()
	prs.slide_width = SLIDE_W
	prs.slide_height = SLIDE_H

	# ---------------------------------------------------------------- Slide 1
	s = add_slide(prs)
	add_text(s, "ARKIK AI CHALLENGE", Inches(0.9), Inches(1.5), Inches(11), Inches(0.5),
			 16, ACCENT, bold=True)
	add_text(s, "Arkik Opportunity Classifier", Inches(0.9), Inches(2.1), Inches(11.5), Inches(1.2),
			 46, WHITE, bold=True)
	add_text(s,
			 "Paste a Jira ticket URL — AI tells you which Arkik opportunity it belongs to,\n"
			 "and highlights the exact sentence that proves it.",
			 Inches(0.9), Inches(3.5), Inches(11.5), Inches(1.2), 20, MUTED, line_spacing=1.3)
	card_with_lines(s, Inches(0.9), Inches(5.0), Inches(5.3), Inches(1.5),
					"Opportunity addressed",
					["Backlog prioritization", "+ Clearer requirements and definitions"])
	card_with_lines(s, Inches(6.5), Inches(5.0), Inches(5.9), Inches(1.5),
					"Working prototype",
					["Live Jira + AI  •  .NET 10 / Blazor", "35 unit tests passing"])

	# ---------------------------------------------------------------- Slide 2
	s = add_slide(prs)
	slide_title(s, "The problem", "Every ticket has to be understood before it can be prioritized")
	card_with_lines(s, Inches(0.7), Inches(2.0), Inches(3.8), Inches(3.9), "Slow",
					["Someone must read a long",
					 "Jira description end-to-end",
					 "before they can categorise it.",
					 "",
					 "2–3 minutes of focus",
					 "per ticket."], title_color=YELLOW)
	card_with_lines(s, Inches(4.75), Inches(2.0), Inches(3.8), Inches(3.9), "Inconsistent",
					["12 people tagging tickets",
					 "by hand produce 12",
					 "different interpretations.",
					 "",
					 "No shared, repeatable",
					 "criteria."], title_color=YELLOW)
	card_with_lines(s, Inches(8.8), Inches(2.0), Inches(3.8), Inches(3.9), "Doesn't scale",
					["The backlog holds",
					 "thousands of tickets.",
					 "",
					 "Nobody can answer:",
					 "'how much of our backlog",
					 "is technical debt?'"], title_color=YELLOW)

	# ---------------------------------------------------------------- Slide 3
	s = add_slide(prs)
	slide_title(s, "The solution", "A working web app — live Jira data, real AI, human-verifiable output")
	steps = [
		("1", "Paste", "Paste any Jira ticket URL."),
		("2", "Fetch", "Reads the live ticket via the Jira REST API."),
		("3", "Classify", "AI picks 1 of the 12 Arkik opportunities."),
		("4", "Prove", "Highlights the sentence that justifies it."),
	]
	left = Inches(0.7)
	for num, title, body in steps:
		card_with_lines(s, left, Inches(2.1), Inches(2.85), Inches(2.6),
						f"{num}.  {title}", ["", body])
		left += Inches(3.05)

	card_with_lines(s, Inches(0.7), Inches(5.0), Inches(11.9), Inches(1.6),
					"Always explainable — never a black box",
					["Confidence score  •  AI-written reason  •  Highlighted quote  "
					 "•  Badge showing AI vs offline fallback",
					 "Click any other opportunity and the AI re-argues that case for the same ticket."],
					title_color=GREEN)

	# ---------------------------------------------------------------- Slide 4
	s = add_slide(prs)
	slide_title(s, "Who it is for, and what they gain")
	card_with_lines(s, Inches(0.7), Inches(1.9), Inches(5.8), Inches(2.1),
					"Intended user",
					["Product Owners and Business Analysts",
					 "who triage the Arkik backlog.",
					 "Also: delivery leads and leadership reporting."])
	card_with_lines(s, Inches(6.8), Inches(1.9), Inches(5.8), Inches(2.1),
					"Headline benefit",
					["2–3 minutes of manual reading  →  ~3 seconds.",
					 "Reviewers jump straight to the highlighted proof",
					 "instead of reading the whole ticket."],
					title_color=GREEN)
	card_with_lines(s, Inches(0.7), Inches(4.2), Inches(3.8), Inches(2.4), "Consistency",
					["One prompt, one set of",
					 "criteria, every ticket.",
					 "Reproducible and auditable."])
	card_with_lines(s, Inches(4.75), Inches(4.2), Inches(3.8), Inches(2.4), "Trust",
					["Confidence + reason + quote",
					 "let a human verify the",
					 "answer in seconds."])
	card_with_lines(s, Inches(8.8), Inches(4.2), Inches(3.8), Inches(2.4), "Low cost",
					["Runs on a free AI tier.",
					 "Keyword fallback keeps it",
					 "working if AI is down."])

	# ---------------------------------------------------------------- Slide 5
	s = add_slide(prs)
	slide_title(s, "Measurable benefit", "Benefit scales linearly with backlog volume")
	add_card(s, Inches(0.7), Inches(1.9), Inches(11.9), Inches(1.5))
	add_text(s, "tickets / month   ×   minutes saved per ticket   ÷   60   =   hours returned / month",
			 Inches(0.9), Inches(2.25), Inches(11.5), Inches(0.8), 22, ACCENT, bold=True,
			 align=PP_ALIGN.CENTER)

	card_with_lines(s, Inches(0.7), Inches(3.7), Inches(3.8), Inches(1.9), "Per ticket",
					["Manual  ≈ 150 sec", "With AI  ≈ 3 sec", "≈ 98% faster"], title_color=GREEN)
	card_with_lines(s, Inches(4.75), Inches(3.7), Inches(3.8), Inches(1.9), "Per 500 tickets",
					["≈ 20 hours of reading", "returned to the team", "every month"], title_color=GREEN)
	card_with_lines(s, Inches(8.8), Inches(3.7), Inches(3.8), Inches(1.9), "Beyond time",
					["Consistent tagging unlocks", "backlog analytics that", "do not exist today."],
					title_color=GREEN)
	add_text(s, "Verified live during the demo: we time a manual read against one click.",
			 Inches(0.7), Inches(5.9), Inches(11.9), Inches(0.5), 14, MUTED)

	# ---------------------------------------------------------------- Slide 6
	s = add_slide(prs)
	slide_title(s, "Live demo", "5 minutes, real ticket ARKIK-42798")
	demo = [
		"1.  Show the app — header confirms it is connected to live Cemex Jira.",
		"2.  Paste the ticket URL and click Analyze.",
		"3.  Read the result: opportunity, green AI badge, confidence, AI-written reason.",
		"4.  Scroll to the yellow highlight — the proof inside the ticket text.",
		"5.  Challenge it: click a different opportunity and watch the AI re-argue.",
	]
	top = Inches(2.0)
	for line in demo:
		card_with_lines(s, Inches(0.7), top, Inches(11.9), Inches(0.82), line, [], title_size=16)
		top += Inches(0.95)

	# ---------------------------------------------------------------- Slide 7
	s = add_slide(prs)
	slide_title(s, "Proof it is really AI", "The model reasons about our actual ticket content")
	card_with_lines(s, Inches(0.7), Inches(1.9), Inches(11.9), Inches(1.9),
					"Classified: Understanding operational needs — confidence 85%",
					['"The ticket focuses on delivering a transparent, traceable action log to meet',
					 'customer and business needs for visibility into order lifecycle events."'],
					title_color=GREEN, body_size=15)
	card_with_lines(s, Inches(0.7), Inches(4.1), Inches(11.9), Inches(2.2),
					"Then we forced the wrong answer: Growing technical debt — confidence 20%",
					['"...it includes a potentially problematic design choice — resetting the action log',
					 'on a customer update — which could be considered a shortcut that adds technical debt."',
					 "",
					 "No keyword matcher could write that sentence. It read our description."],
					title_color=YELLOW, body_size=15)

	# ---------------------------------------------------------------- Slide 8
	s = add_slide(prs)
	slide_title(s, "What's next", "If adopted beyond the hackathon")
	nxt = [
		("Bulk classification", "Run across a whole JQL query and chart the backlog:\n"
								"'38% of our backlog is technical debt.'"),
		("Write back to Jira", "Set a label or custom field automatically,\nso the insight lives in the ticket."),
		("Per-user sign-in", "Atlassian OAuth so each user sees only\nthe tickets they are permitted to."),
		("Tune on real data", "Improve accuracy using tickets\nalready labelled by the team."),
	]
	left, top = Inches(0.7), Inches(2.0)
	for i, (title, body) in enumerate(nxt):
		card_with_lines(s, left, top, Inches(5.8), Inches(2.0), title, [body])
		if i % 2 == 0:
			left = Inches(6.8)
		else:
			left, top = Inches(0.7), top + Inches(2.25)

	# ---------------------------------------------------------------- Slide 9
	s = add_slide(prs)
	add_text(s, "Thank you", Inches(0.9), Inches(2.4), Inches(11.5), Inches(1.2), 44, WHITE, bold=True)
	add_text(s,
			 "Arkik Opportunity Classifier — a working AI prototype for backlog prioritization.",
			 Inches(0.9), Inches(3.6), Inches(11.5), Inches(0.8), 20, MUTED)
	card_with_lines(s, Inches(0.9), Inches(4.6), Inches(11.5), Inches(1.6), "Questions?",
					["Live app + REST API + Swagger  •  35 unit tests  •  free AI tier  "
					 "•  works offline via fallback"])

	out_dir = Path(__file__).resolve().parent.parent / "docs"
	out_dir.mkdir(exist_ok=True)
	out = out_dir / "Arkik-Opportunity-Classifier.pptx"
	prs.save(out)
	print(f"Created {out}")


if __name__ == "__main__":
	build()
