import os
from PIL import Image, ImageOps

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
INPUT_IMAGE = os.path.join(BASE_DIR, "app.png")

SMALL_SIZE = (55, 55)
SIDE_SIZE = (164, 314)

def create_image(input_path, output_path, size):
    img = Image.open(input_path).convert("RGBA")

    img_ratio = img.width / img.height
    target_ratio = size[0] / size[1]

    if img_ratio > target_ratio:
        new_height = size[1]
        new_width = int(new_height * img_ratio)
    else:
        new_width = size[0]
        new_height = int(new_width / img_ratio)

    img = img.resize((new_width, new_height), Image.LANCZOS)

    img = ImageOps.fit(img, size, Image.LANCZOS, centering=(0.5, 0.5))

    background = Image.new("RGB", size, (255, 255, 255))
    background.paste(img, mask=img.split()[3])

    background.save(output_path, "BMP")


create_image(INPUT_IMAGE, "small.bmp", SMALL_SIZE)
create_image(INPUT_IMAGE, "side.bmp", SIDE_SIZE)

print("Images generated: small.bmp and side.bmp")