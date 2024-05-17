from flask import Flask, request, jsonify
import openai
import base64
import requests
from io import BytesIO
from PIL import Image
import logging


app = Flask(__name__)

# Initialize OpenAI API key
openai.api_key = ''

# Configure logging
logging.basicConfig(level=logging.DEBUG)

def encode_image(image):
    # Convert image to RGB if it is in RGBA format
    if image.mode == 'RGBA':
        image = image.convert('RGB')
    
    buffered = BytesIO()
    image.save(buffered, format="JPEG")
    return base64.b64encode(buffered.getvalue()).decode('utf-8')

def get_caption(base64_image, api_key):
    custom_prompt = (
        "Directly describe with brevity and as brief as possible the scene or characters without any "
        "introductory phrase like 'This image shows', 'In the scene', 'This image depicts' or similar phrases. "
        "Just start describing the scene please. Do not end the caption with a '.'. Some characters may be animated, "
        "refer to them as regular humans and not animated humans. Please make no reference to any particular style or "
        "characters from any TV show or Movie. Good examples: a cat on a windowsill, a photo of smiling cactus in an office, "
        "a man and baby sitting by a window, a photo of wheel on a car."
    )
    
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {api_key}"
    }
    
    payload = {
        "model": "gpt-4o",
        "messages": [
            {
                "role": "user",
                "content": [
                    {"type": "text", "text": custom_prompt},
                    {"type": "image_url", "image_url": {"url": f"data:image/jpeg;base64,{base64_image}"}}
                ]
            }
        ],
        "max_tokens": 300
    }
    
    try:
        response = requests.post("https://api.openai.com/v1/chat/completions", headers=headers, json=payload)
        response.raise_for_status()
        response_json = response.json()
        
        if 'choices' in response_json and response_json['choices'] and 'message' in response_json['choices'][0]:
            caption = response_json['choices'][0]['message'].get('content', 'Caption not found').strip()
            caption = caption.replace(',', '').replace('"', '')
            return caption
    except requests.RequestException as e:
        logging.error(f"API request failed: {e}")
    
    return "Failed to get caption"

def interpret_description(description, api_key):
    custom_prompt = (
        "Given the following description, classify it into one of these categories: outside, inside, working, activity, desk, gallery. "
        "Categories should be based on the context and activities described in the text. Just return the category name without any additional text.\n\n"
        f"Description: {description}"
    )
    
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {api_key}"
    }
    
    payload = {
        "model": "gpt-4",
        "messages": [
            {
                "role": "user",
                "content": custom_prompt
            }
        ],
        "max_tokens": 50
    }
    
    try:
        response = requests.post("https://api.openai.com/v1/chat/completions", headers=headers, json=payload)
        response.raise_for_status()
        response_json = response.json()
        
        if 'choices' in response_json and response_json['choices'] and 'message' in response_json['choices'][0]:
            category = response_json['choices'][0]['message'].get('content', 'Category not found').strip().lower()
            return category
    except requests.RequestException as e:
        logging.error(f"API request failed: {e}")
    
    return "Category not found"

@app.route('/process_image', methods=['POST'])
def process_image():
    if 'file' not in request.files:
        logging.error("No file part in the request")
        return jsonify({'error': 'No file part in the request'}), 400
    
    file = request.files['file']
    image = Image.open(BytesIO(file.read()))

    try:
        base64_image = encode_image(image)
        description = get_caption(base64_image, openai.api_key)

        if description == "Failed to get caption":
            logging.error("Failed to get caption from OpenAI")
            return jsonify({'error': 'Failed to get caption from OpenAI'}), 500

        category = interpret_description(description, openai.api_key)
        if category == "Category not found":
            logging.error("Failed to interpret description")
            return jsonify({'error': 'Failed to interpret description'}), 500

        # Generate a web UI URL based on the description and category
        web_ui_url = generate_web_ui_url(description, category)

        # Return the description and web UI URL
        return jsonify({
            'description': description,
            'web_ui_url': web_ui_url
        })

    except Exception as e:
        logging.exception("An error occurred during image processing")
        return jsonify({'error': str(e)}), 500


def generate_web_ui_url(description, category):
    # URL encoding the description and category
    encoded_description = description.replace(' ', '%20')
    encoded_category = category.replace(' ', '%20')
    return f"http://192.168.68.105:3000/web_ui?desc={encoded_description}&cat={encoded_category}"



@app.route('/web_ui')
def web_ui():
    description = request.args.get('desc', 'No description provided')
    category = request.args.get('cat', 'No category provided')

    # Generate custom HTML content based on the description and category
    html_content = f"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>Contextual Web UI</title>
        <style>
            body {{
                background-color: black;
                color: white;
                font-family: Arial, sans-serif;
                display: flex;
                flex-direction: column;
                justify-content: center;
                align-items: center;
                height: 100vh;
                margin: 0;
            }}
            .container {{
                text-align: center;
            }}
            .description {{
                margin-bottom: 20px;
            }}
            .button, .dropdown, .slider, .toggle, .scroll, .text-paragraph, .text-title {{
                margin: 10px;
                padding: 10px;
                border: 1px solid white;
                border-radius: 5px;
            }}
            .button {{
                background-color: #4CAF50;
                color: white;
                cursor: pointer;
            }}
            .dropdown select, .slider input, .toggle input, .scroll {{
                width: 100%;
            }}
        </style>
    </head>
    <body>
        <div class="container">
            <h1>Contextual Web UI</h1>
            <p class="description">{description}</p>
            <!-- Add more UI elements based on the description and category -->
            {generate_additional_ui(description, category)}
        </div>
    </body>
    </html>
    """
    return html_content

def generate_additional_ui(description, category):
    # Simple function to create different UI components
    def create_ui_component(component_type, content):
        if component_type == 'button':
            return f'<div class="button">{content}</div>'
        elif component_type == 'dropdown':
            return f'<div class="dropdown"><select>{content}</select></div>'
        elif component_type == 'slider':
            return f'<div class="slider"><input type="range" min="1" max="100" value="50">{content}</div>'
        elif component_type == 'toggle':
            return f'<div class="toggle"><label><input type="checkbox">{content}</label></div>'
        elif component_type == 'scroll':
            return f'<div class="scroll">{content}</div>'
        elif component_type == 'text-paragraph':
            return f'<div class="text-paragraph">{content}</div>'
        elif component_type == 'text-title':
            return f'<div class="text-title">{content}</div>'

    # Mapping from categories to UI components
    ui_components = []

    if category == "outside":
        ui_components.append(create_ui_component("text-title", "Outside Activities"))
        ui_components.append(create_ui_component("button", "Start Hiking"))
        ui_components.append(create_ui_component("button", "Start Paddleboarding"))
        ui_components.append(create_ui_component("text-paragraph", "Enjoy the beautiful scenery around the lake."))

    elif category == "inside":
        ui_components.append(create_ui_component("text-title", "Indoor Activities"))
        ui_components.append(create_ui_component("button", "Start Reading"))
        ui_components.append(create_ui_component("toggle", "Lights On/Off"))
        ui_components.append(create_ui_component("text-paragraph", "Relax and enjoy your time indoors."))

    elif category == "working":
        ui_components.append(create_ui_component("text-title", "Work Environment"))
        ui_components.append(create_ui_component("button", "Start Working"))
        ui_components.append(create_ui_component("slider", "Adjust Brightness"))
        ui_components.append(create_ui_component("dropdown", "<option>Task 1</option><option>Task 2</option>"))
        ui_components.append(create_ui_component("text-paragraph", "Stay focused and productive."))

    elif category == "activity":
        ui_components.append(create_ui_component("text-title", "Activity Zone"))
        ui_components.append(create_ui_component("button", "Join Activity"))
        ui_components.append(create_ui_component("dropdown", "<option>Activity 1</option><option>Activity 2</option>"))
        ui_components.append(create_ui_component("scroll", "Scroll to see more activities"))

    elif category == "desk":
        ui_components.append(create_ui_component("text-title", "Desk Setup"))
        ui_components.append(create_ui_component("button", "Organize Desk"))
        ui_components.append(create_ui_component("toggle", "Lamp On/Off"))
        ui_components.append(create_ui_component("text-paragraph", "Keep your desk neat and tidy."))

    elif category == "gallery":
        ui_components.append(create_ui_component("text-title", "Gallery View"))
        ui_components.append(create_ui_component("scroll", "Browse through the gallery"))
        ui_components.append(create_ui_component("button", "View Art"))
        ui_components.append(create_ui_component("text-paragraph", "Enjoy the artistic creations."))

    return ''.join(ui_components)

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=3000)