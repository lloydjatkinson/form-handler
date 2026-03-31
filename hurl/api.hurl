# Run:       hurl --variables-file hurl/vars.env hurl/api.hurl
# Run tests: hurl --test --variables-file hurl/vars.env hurl/api.hurl


##############################
# GET /forms/{formId}
##############################

GET {{base}}/forms/inbox
HTTP 200

GET {{base}}/forms/unknown
HTTP 404


##############################
# POST /forms/inbox/submissions — JSON, valid
##############################

# Required fields only
POST {{base}}/forms/inbox/submissions
Content-Type: application/json
{"firstName":"Alice","lastName":"Smith","email":"alice@market.example","message":"Hello"}
HTTP 201

# All optional fields included
POST {{base}}/forms/inbox/submissions
Content-Type: application/json
{"firstName":"Alice","lastName":"Smith","company":"Acme Ltd","email":"alice@market.example","message":"Hello there"}
HTTP 201


##############################
# POST /forms/inbox/submissions — JSON, validation failures
##############################

# Required field missing
POST {{base}}/forms/inbox/submissions
Content-Type: application/json
{"firstName":"Alice"}
HTTP 400

# Null on required field
POST {{base}}/forms/inbox/submissions
Content-Type: application/json
{"firstName":"Alice","lastName":"Smith","email":null,"message":"Hello"}
HTTP 400

# Unknown field
POST {{base}}/forms/inbox/submissions
Content-Type: application/json
{"firstName":"Alice","lastName":"Smith","email":"alice@market.example","message":"Hello","utm_source":"flyer"}
HTTP 400

# Unknown form
POST {{base}}/forms/unknown/submissions
Content-Type: application/json
{"firstName":"Alice","lastName":"Smith","email":"alice@market.example","message":"Hello"}
HTTP 404


##############################
# POST /forms/inbox/submissions — form-urlencoded, valid
##############################

POST {{base}}/forms/inbox/submissions
[FormParams]
firstName: Alice
lastName: Smith
email: alice@market.example
message: Hello
HTTP 201


##############################
# POST /forms/inbox/submissions — form-urlencoded, validation failures
##############################

# Required fields missing
POST {{base}}/forms/inbox/submissions
[FormParams]
firstName: Alice
HTTP 400

# Unknown field
POST {{base}}/forms/inbox/submissions
[FormParams]
firstName: Alice
lastName: Smith
email: alice@market.example
message: Hello
sneaky_field: should be rejected
HTTP 400

# Unknown form
POST {{base}}/forms/unknown/submissions
[FormParams]
firstName: Alice
HTTP 404


##############################
# Content type and parse edge cases
##############################

# Wrong content type — JSON body sent as text/plain
POST {{base}}/forms/inbox/submissions
Content-Type: text/plain
{"firstName":"Alice","lastName":"Smith","email":"alice@market.example","message":"Hello"}
HTTP 415

# Empty body
POST {{base}}/forms/inbox/submissions
Content-Type: application/json
``
HTTP 400

# null root
POST {{base}}/forms/inbox/submissions
Content-Type: application/json
null
HTTP 400
