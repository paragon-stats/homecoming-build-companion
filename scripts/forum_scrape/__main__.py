"""CLI entry point: ``python -m scripts.forum_scrape``."""

import sys

from scripts.forum_scrape.scrape import main

if __name__ == "__main__":
    sys.exit(main())
